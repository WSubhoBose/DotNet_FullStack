using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace BreweryApi.Auth.Services;

internal sealed class CustomTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Auth", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        if (headerValues.Count != 1 || string.IsNullOrWhiteSpace(headerValues[0]))
        {
            return AuthenticateResult.Fail("The Auth header must contain one access token.");
        }

        var token = headerValues[0]!;
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var configuredPath = configuration["Auth:DatabasePath"] ?? "../../data/auth.db";
        var path = Path.GetFullPath(configuredPath, environment.ContentRootPath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Context.RequestAborted);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Subject
            FROM AccessTokens
            WHERE TokenHash = $hash
              AND RevokedAtUtc IS NULL
              AND ExpiresAtUtc > $now
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$hash", tokenHash);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        var subject = await command.ExecuteScalarAsync(Context.RequestAborted) as string;
        if (subject is null)
        {
            return AuthenticateResult.Fail("The Auth token is not active.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject)],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
