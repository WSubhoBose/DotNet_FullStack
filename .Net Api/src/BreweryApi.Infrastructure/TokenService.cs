using System.Security.Cryptography;
using System.Text;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BreweryApi.Infrastructure;

public sealed class TokenService(IDbContextFactory<AuthDbContext> contextFactory) : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<TokenResponse?> CreateAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Username == request.Username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var (accessToken, refreshToken, expiresAt, refreshExpiresAt) = CreateTokenPair();
        var now = DateTimeOffset.UtcNow;
        var nowText = now.ToString("O");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var accessTokens = await db.AccessTokens.ToListAsync(cancellationToken);
        db.AccessTokens.RemoveRange(accessTokens.Where(token =>
            token.RevokedAtUtc != null ||
            DateTimeOffset.Parse(token.ExpiresAtUtc, System.Globalization.CultureInfo.InvariantCulture) <= now));
        var refreshTokens = await db.RefreshTokens.ToListAsync(cancellationToken);
        db.RefreshTokens.RemoveRange(refreshTokens.Where(token =>
            token.RevokedAtUtc != null ||
            DateTimeOffset.Parse(token.ExpiresAtUtc, System.Globalization.CultureInfo.InvariantCulture) <= now));

        db.AccessTokens.Add(new AccessTokenRecord
        {
            TokenHash = HashToken(accessToken),
            Subject = request.Username,
            CreatedAtUtc = nowText,
            ExpiresAtUtc = expiresAt.ToString("O")
        });
        db.RefreshTokens.Add(new RefreshTokenRecord
        {
            TokenHash = HashToken(refreshToken),
            Subject = request.Username,
            CreatedAtUtc = nowText,
            ExpiresAtUtc = refreshExpiresAt.ToString("O")
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TokenResponse(accessToken, refreshToken, "Auth", expiresAt, refreshExpiresAt, user.CanUseAutocomplete);
    }

    public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var storedToken = await db.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.RevokedAtUtc == null,
                cancellationToken);
        if (storedToken is null ||
            DateTimeOffset.Parse(storedToken.ExpiresAtUtc, System.Globalization.CultureInfo.InvariantCulture) <= now)
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Username == storedToken.Subject, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var (accessToken, nextRefreshToken, expiresAt, refreshExpiresAt) = CreateTokenPair();
        storedToken.RevokedAtUtc = now.ToString("O");
        db.AccessTokens.Add(new AccessTokenRecord
        {
            TokenHash = HashToken(accessToken),
            Subject = storedToken.Subject,
            CreatedAtUtc = now.ToString("O"),
            ExpiresAtUtc = expiresAt.ToString("O")
        });
        db.RefreshTokens.Add(new RefreshTokenRecord
        {
            TokenHash = HashToken(nextRefreshToken),
            Subject = storedToken.Subject,
            CreatedAtUtc = now.ToString("O"),
            ExpiresAtUtc = refreshExpiresAt.ToString("O")
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TokenResponse(accessToken, nextRefreshToken, "Auth", expiresAt, refreshExpiresAt, user.CanUseAutocomplete);
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
}
