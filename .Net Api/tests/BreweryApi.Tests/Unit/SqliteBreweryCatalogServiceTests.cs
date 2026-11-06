using BreweryApi.Domain.Helpers;
using BreweryApi.Domain;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Infrastructure;
using BreweryApi.Infrastructure.Persistence;
using BreweryApi.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class SqliteBreweryCatalogServiceTests
{
    [Fact]
    public async Task GetAsync_HandlesNullRequiredTextFromUpstream()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"brewery-catalog-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:SqlitePath"] = databasePath
                })
                .Build();
            var openBreweryClient = new Mock<IOpenBreweryClient>();
            openBreweryClient
                .Setup(client => client.QueryAsync(
                    It.IsAny<BreweryQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((BreweryQuery query, CancellationToken _) =>
                    new PagedResponse<BreweryResponse>(
                        [new BreweryResponse(
                            "test-1",
                            null!,
                            "micro",
                            null,
                            null,
                            null,
                            "Denver",
                            null!,
                            null!,
                            "United States",
                            null,
                            null,
                            null,
                            null,
                            null,
                            null)],
                        query.Page,
                        query.PageSize,
                        null));
            var service = new SqliteBreweryCatalogService(
                CreateContextFactory(databasePath),
                openBreweryClient.Object,
                new DistanceCalculator(),
                configuration,
                NullLogger<SqliteBreweryCatalogService>.Instance);

            var response = await service.GetAsync(new BreweryQuery(), CancellationToken.None);

            var brewery = Assert.Single(response.Breweries);
            Assert.Empty(brewery.Name);
            Assert.Empty(brewery.StateProvince);
            Assert.Empty(brewery.PostalCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task GetAsync_FiltersAndPaginatesPersistedBreweryRecords()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"brewery-catalog-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:SqlitePath"] = databasePath
                })
                .Build();
            var service = new SqliteBreweryCatalogService(
                CreateContextFactory(databasePath),
                new TestOpenBreweryClient(),
                new DistanceCalculator(),
                configuration,
                NullLogger<SqliteBreweryCatalogService>.Instance);

            var response = await service.GetAsync(
                new BreweryApi.Domain.Models.BreweryQuery(City: "Denver", SortBy: "name", PageSize: 1),
                CancellationToken.None);
            var suggestions = await service.AutocompleteAsync("den", 10, CancellationToken.None);

            Assert.Equal(1, response.TotalCount);
            Assert.Single(response.Breweries);
            Assert.Equal("Alpha Brewing", response.Breweries[0].Name);
            Assert.Contains("Denver", suggestions);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static IDbContextFactory<BreweryDbContext> CreateContextFactory(string databasePath)
    {
        var options = new DbContextOptionsBuilder<BreweryDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new PooledDbContextFactory<BreweryDbContext>(options);
    }
}
