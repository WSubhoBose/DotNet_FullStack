using System.Security.Cryptography;
using System.Text;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Infrastructure.Persistence;
using BreweryApi.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BreweryApi.Tests.Integration;

public sealed class BreweryApiFactory : WebApplicationFactory<Program>
{
    private readonly string authDatabasePath = Path.Combine(Path.GetTempPath(), $"brewery-api-tests-{Guid.NewGuid():N}.db");
    private readonly string catalogDatabasePath = Path.Combine(Path.GetTempPath(), $"brewery-catalog-tests-{Guid.NewGuid():N}.db");

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "user1")
    {
        var client = CreateClient();
        var issuedAt = DateTimeOffset.UtcNow;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = issuedAt.AddMinutes(5);

        var contextFactory = Services.GetRequiredService<IDbContextFactory<AuthDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();
        db.AccessTokens.Add(new AccessTokenRecord
        {
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            Subject = username,
            CreatedAtUtc = issuedAt.ToString("O"),
            ExpiresAtUtc = expiresAt.ToString("O")
        });
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Add("Auth", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:DatabasePath"] = authDatabasePath,
                    ["Data:SqlitePath"] = catalogDatabasePath
                });
            });

        builder.ConfigureTestServices(
            services =>
            {
                services.RemoveAll<IOpenBreweryClient>();
                services.AddSingleton<IOpenBreweryClient, TestOpenBreweryClient>();
            });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteDatabaseFiles(authDatabasePath);
            DeleteDatabaseFiles(catalogDatabasePath);
        }
    }

    private static void DeleteDatabaseFiles(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var filePath = path + suffix;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}