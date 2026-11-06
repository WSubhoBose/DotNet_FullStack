using System.Security.Cryptography;
using System.Text;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BreweryApi.Infrastructure;

public sealed class SqliteActiveTokenStore(
    IDbContextFactory<AuthDbContext> contextFactory) : IActiveTokenStore
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        foreach (var (username, canUseAutocomplete) in new[]
        {
            ("user1", true),
            ("user2", false)
        })
        {
            if (!await db.Users.AnyAsync(user => user.Username == username, cancellationToken))
            {
                db.Users.Add(new AuthUserRecord
                {
                    Username = username,
                    CanUseAutocomplete = canUseAutocomplete
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsActiveAsync(string token, CancellationToken cancellationToken) =>
        await GetActiveUserAsync(token, cancellationToken) is not null;

    public async Task<ActiveTokenUser?> GetActiveUserAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var tokenHash = HashToken(token);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var accessToken = await db.AccessTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.TokenHash == tokenHash && value.RevokedAtUtc == null,
                cancellationToken);
        if (accessToken is null ||
            DateTimeOffset.Parse(accessToken.ExpiresAtUtc, System.Globalization.CultureInfo.InvariantCulture) <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Username == accessToken.Subject, cancellationToken);
        return user is null ? null : new ActiveTokenUser(user.Username, user.CanUseAutocomplete);
    }

    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
