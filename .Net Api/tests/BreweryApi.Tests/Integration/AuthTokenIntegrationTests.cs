using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BreweryApi.Domain.Entities;
using BreweryApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BreweryApi.Tests.Integration;

public sealed class AuthTokenIntegrationTests
{
    private const string Username = "user1";
    [Fact]
    public async Task TokenEndpoint_ValidatesUsernameAndRefreshTokensRotateOnce()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"brewery-auth-tests-{Guid.NewGuid():N}.db");
        using var factory = new AuthApiFactory(databasePath);
        using var client = factory.CreateClient();
        Assert.Equal(databasePath, factory.Services.GetRequiredService<IConfiguration>()["Auth:DatabasePath"]);

        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var swaggerDocument = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        var swaggerPaths = swaggerDocument.RootElement.GetProperty("paths");
        Assert.True(swaggerPaths.TryGetProperty("/api/auth/token", out _));

        using var rejected = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { username = "unknown-user", password = "any string" });
        using var response = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { username = Username, password = "the password value is ignored" });
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from token endpoint; got {(int)response.StatusCode}: {responseContent}");
        var token = JsonSerializer.Deserialize<TokenResponse>(
            responseContent,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Contains("This username is not registered.", await rejected.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(token);
        Assert.Equal("Auth", token.TokenType);
        Assert.True(token.CanUseAutocomplete);
        Assert.DoesNotContain(".", token.AccessToken);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.InRange(
            token.ExpiresAt - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(9),
            TimeSpan.FromMinutes(10));
        Assert.True(token.RefreshExpiresAt > token.ExpiresAt);
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        var tokenStore = factory.Services.GetRequiredService<BreweryApi.Domain.Interfaces.IActiveTokenStore>();
        Assert.True(await tokenStore.IsActiveAsync(token.AccessToken, CancellationToken.None));

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<AuthDbContext>>();
        await using var db = await contextFactory.CreateDbContextAsync();
        Assert.Equal(databasePath, db.Database.GetDbConnection().DataSource);
        var accessToken = Assert.Single(await db.AccessTokens.AsNoTracking().ToListAsync());
        Assert.Equal(Username, accessToken.Subject);
        Assert.Equal(token.ExpiresAt, DateTimeOffset.Parse(accessToken.ExpiresAtUtc));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.AccessToken))),
            accessToken.TokenHash);

        var users = await db.Users.AsNoTracking().OrderBy(user => user.Username).ToListAsync();
        Assert.Collection(
            users,
            user =>
            {
                Assert.Equal("user1", user.Username);
                Assert.True(user.CanUseAutocomplete);
            },
            user =>
            {
                Assert.Equal("user2", user.Username);
                Assert.False(user.CanUseAutocomplete);
            });

        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = "PRAGMA table_info(Users);";
            await using var schemaReader = await schemaCommand.ExecuteReaderAsync();
            var userColumns = new List<string>();
            while (await schemaReader.ReadAsync())
            {
                userColumns.Add(schemaReader.GetString(1));
            }

            Assert.DoesNotContain("Password", userColumns);
        }

        using var refreshedResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        var refreshedToken = await refreshedResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(refreshedToken);
        Assert.NotEqual(token.AccessToken, refreshedToken.AccessToken);
        Assert.NotEqual(token.RefreshToken, refreshedToken.RefreshToken);
        Assert.True(refreshedToken.CanUseAutocomplete);
        Assert.True(await tokenStore.IsActiveAsync(refreshedToken.AccessToken, CancellationToken.None));

        using var reusedRefresh = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reusedRefresh.StatusCode);
    }

    [Theory]
    [InlineData("user1", HttpStatusCode.BadRequest)]
    [InlineData("user2", HttpStatusCode.Forbidden)]
    public async Task Autocomplete_AppliesUserPermission(string username, HttpStatusCode expectedStatus)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"brewery-auth-tests-{Guid.NewGuid():N}.db");
        using var factory = new AuthApiFactory(databasePath);
        using var client = factory.CreateClient();

        using var tokenResponse = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { username, password = "any string" });
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.Equal(username == "user1", token.CanUseAutocomplete);
        client.DefaultRequestHeaders.Add("Auth", token.AccessToken);

        using var response = await client.GetAsync("/api/v2/breweries/autocomplete");

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Your account cannot use the autocomplete API.", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TokenEndpoint_ExplainsMissingOrInvalidAuthentication()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"brewery-auth-tests-{Guid.NewGuid():N}.db");
        using var factory = new AuthApiFactory(databasePath);
        using var client = factory.CreateClient();

        using var rejected = await client.GetAsync("/api/v2/breweries");
        using var response = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { username = "unknown-user", password = "any string" });
        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Contains("Send a valid access token in the Auth header.", rejectedBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("This username is not registered.", responseBody, StringComparison.Ordinal);
    }

    private sealed class AuthApiFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:DatabasePath"] = databasePath,
                    ["Data:SqlitePath"] = Path.Combine(Path.GetTempPath(), $"brewery-catalog-tests-{Guid.NewGuid():N}.db")
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                SqliteConnection.ClearAllPools();
                DeleteDatabaseFiles(databasePath);
            }
        }
    }

    private static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var filePath = databasePath + suffix;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
