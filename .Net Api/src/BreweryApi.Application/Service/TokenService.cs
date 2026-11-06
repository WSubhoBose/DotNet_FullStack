using System.Security.Cryptography;
using System.Text;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BreweryApi.Application.Service;

public sealed class TokenService : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private readonly IConfiguration configuration;
    private readonly string connectionString;

    public TokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        this.configuration = configuration;
        connectionString = CreateConnectionString(environment);
    }

    public async Task<TokenResponse?> CreateAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        bool canUseAutocomplete;
        await using (var userCommand = connection.CreateCommand())
        {
            userCommand.CommandText = "SELECT CanUseAutocomplete FROM Users WHERE Username = $username;";
            userCommand.Parameters.AddWithValue("$username", request.Username);
            var userExists = await userCommand.ExecuteScalarAsync(cancellationToken);
            if (userExists is null)
            {
                return null;
            }

            canUseAutocomplete = Convert.ToInt32(userExists, System.Globalization.CultureInfo.InvariantCulture) == 1;
        }

        var (accessToken, refreshToken, expiresAt, refreshExpiresAt) = CreateTokenPair();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM AccessTokens WHERE ExpiresAtUtc <= $now OR RevokedAtUtc IS NOT NULL;
            DELETE FROM RefreshTokens WHERE ExpiresAtUtc <= $now OR RevokedAtUtc IS NOT NULL;
            INSERT INTO AccessTokens (TokenHash, Subject, CreatedAtUtc, ExpiresAtUtc)
            VALUES ($hash, $subject, $createdAt, $expiresAt);
            INSERT INTO RefreshTokens (TokenHash, Subject, CreatedAtUtc, ExpiresAtUtc)
            VALUES ($refreshHash, $subject, $createdAt, $refreshExpiresAt);
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$hash", HashToken(accessToken));
        command.Parameters.AddWithValue("$subject", request.Username);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$expiresAt", expiresAt.ToString("O"));
        command.Parameters.AddWithValue("$refreshHash", HashToken(refreshToken));
        command.Parameters.AddWithValue("$refreshExpiresAt", refreshExpiresAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new TokenResponse(accessToken, refreshToken, "Auth", expiresAt, refreshExpiresAt, canUseAutocomplete);
    }

    public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var now = DateTimeOffset.UtcNow;
        var (accessToken, nextRefreshToken, expiresAt, refreshExpiresAt) = CreateTokenPair();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);

        string? subject;
        bool canUseAutocomplete;
        await using (var findCommand = connection.CreateCommand())
        {
            findCommand.Transaction = transaction;
            findCommand.CommandText = """
                SELECT RefreshTokens.Subject, RefreshTokens.ExpiresAtUtc, Users.CanUseAutocomplete
                FROM RefreshTokens
                INNER JOIN Users ON Users.Username = RefreshTokens.Subject
                WHERE RefreshTokens.TokenHash = $hash AND RefreshTokens.RevokedAtUtc IS NULL;
                """;
            findCommand.Parameters.AddWithValue("$hash", HashToken(refreshToken));
            await using var reader = await findCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture) <= now)
            {
                return null;
            }

            subject = reader.GetString(0);
            canUseAutocomplete = reader.GetInt64(2) == 1;
        }

        await using (var revokeCommand = connection.CreateCommand())
        {
            revokeCommand.Transaction = transaction;
            revokeCommand.CommandText = """
                UPDATE RefreshTokens SET RevokedAtUtc = $now
                WHERE TokenHash = $hash AND RevokedAtUtc IS NULL AND ExpiresAtUtc > $now;
                """;
            revokeCommand.Parameters.AddWithValue("$now", now.ToString("O"));
            revokeCommand.Parameters.AddWithValue("$hash", HashToken(refreshToken));
            if (await revokeCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return null;
            }
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO AccessTokens (TokenHash, Subject, CreatedAtUtc, ExpiresAtUtc)
                VALUES ($accessHash, $subject, $createdAt, $accessExpiresAt);
                INSERT INTO RefreshTokens (TokenHash, Subject, CreatedAtUtc, ExpiresAtUtc)
                VALUES ($refreshHash, $subject, $createdAt, $refreshExpiresAt);
                """;
            insertCommand.Parameters.AddWithValue("$accessHash", HashToken(accessToken));
            insertCommand.Parameters.AddWithValue("$refreshHash", HashToken(nextRefreshToken));
            insertCommand.Parameters.AddWithValue("$subject", subject);
            insertCommand.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            insertCommand.Parameters.AddWithValue("$accessExpiresAt", expiresAt.ToString("O"));
            insertCommand.Parameters.AddWithValue("$refreshExpiresAt", refreshExpiresAt.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TokenResponse(accessToken, nextRefreshToken, "Auth", expiresAt, refreshExpiresAt, canUseAutocomplete);
    }

    private static (string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, DateTimeOffset RefreshExpiresAt)
        CreateTokenPair()
    {
        var issuedAt = DateTimeOffset.UtcNow;
        return (
            CreateRandomToken(),
            CreateRandomToken(),
            issuedAt.Add(AccessTokenLifetime),
            issuedAt.Add(RefreshTokenLifetime));
    }

    private static string CreateRandomToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string CreateConnectionString(IHostEnvironment environment)
    {
        var configuredPath = configuration["Auth:DatabasePath"] ?? "../../data/auth.db";
        var path = Path.GetFullPath(configuredPath, environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }
}
