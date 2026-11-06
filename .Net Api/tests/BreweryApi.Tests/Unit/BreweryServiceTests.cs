using BreweryApi.Application;
using BreweryApi.Application.Service;
using BreweryApi.Domain;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Tests.TestData;

using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class BreweryServiceTests
{
    private readonly TestBreweryCache cache = new();

    private readonly Mock<IOpenBreweryClient> clientMock =
        new(MockBehavior.Strict);

    private readonly Mock<ILogger<BreweryService>> loggerMock =
        new();


    [Fact]
    public async Task GetAsync_WhenCacheContainsData_DoesNotCallApi()
    {
        var query = CreateQuery();

        var cachedResponse = CreatePagedResponse(
            query,
            [BreweryTestData.CreateResponse(
                id: "1",
                name: "Cached Brewery",
                city: "Denver")]);

        cache.Seed(
            BreweryCacheKey.Create(query),
            cachedResponse);

        var result = await CreateSut().GetAsync(
            query,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Same(cachedResponse, result);
        Assert.Single(result.Breweries);

        clientMock.Verify(
            client => client.QueryAsync(
                It.IsAny<BreweryQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsCachedResponse()
    {
        var query = CreateQuery(
            city: "Denver",
            sortBy: "name",
            sortDirection: "asc",
            page: 1,
            pageSize: 20);

        var cachedResponse = CreatePagedResponse(
            query,
            [
                BreweryTestData.CreateResponse(
                    id: "1",
                    name: "Alpha Brewing",
                    city: "Denver"),
                BreweryTestData.CreateResponse(
                    id: "2",
                    name: "Central Brewing",
                    city: "Denver")
            ]);

        cache.Seed(
            BreweryCacheKey.Create(query),
            cachedResponse);

        var result = await CreateSut().GetAsync(
            query,
            CancellationToken.None);

        Assert.Same(cachedResponse, result);
        Assert.Equal(2, result.Breweries.Count);

        clientMock.Verify(
            client => client.QueryAsync(
                It.IsAny<BreweryQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_QueriesSourceAndCachesBoundedResponse()
    {
        var query = CreateQuery(
            city: "Denver",
            sortBy: "name",
            sortDirection: "asc",
            page: 1,
            pageSize: 20);

        var externalResponse = CreatePagedResponse(
            query,
            [
                BreweryTestData.CreateResponse(
                    id: "1",
                    name: "Alpha Brewing",
                    city: "Denver"),
                BreweryTestData.CreateResponse(
                    id: "2",
                    name: "Central Brewing",
                    city: "Denver")
            ]);

        clientMock
            .Setup(client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalResponse);

        var result = await CreateSut().GetAsync(
            query,
            CancellationToken.None);

        Assert.Same(externalResponse, result);

        clientMock.Verify(
            client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);

        var cacheKey = BreweryCacheKey.Create(query);

        var found = cache.TryGet<PagedResponse<BreweryResponse>>(
            cacheKey,
            out var cachedResponse);

        Assert.True(found);
        Assert.NotNull(cachedResponse);
        Assert.Same(externalResponse, cachedResponse);
    }

    [Fact]
    public async Task GetAsync_DistanceSort_DelegatesToExternalApiAndCachesResponse()
    {
        var query = CreateQuery(
            sortBy: "distance",
            latitude: 32.7157m,
            longitude: -117.1611m);

        var expected = CreatePagedResponse(
            query,
            [BreweryTestData.CreateResponse(
                id: "1",
                name: "Nearest Brewery",
                city: "San Diego")],
            totalCount: null);

        clientMock
            .Setup(client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateSut().GetAsync(
            query,
            CancellationToken.None);

        Assert.Same(expected, result);

        clientMock.Verify(
            client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(
            cache.Contains(BreweryCacheKey.Create(query)));
    }

    [Fact]
    public async Task GetAsync_SameQueryAfterFirstRequest_UsesCachedResponse()
    {
        var query = CreateQuery(
            search: "brew",
            page: 1,
            pageSize: 10);

        var externalResponse = CreatePagedResponse(
            query,
            [BreweryTestData.CreateResponse(
                id: "1",
                name: "Brew House",
                city: "Austin")]);

        clientMock
            .Setup(client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalResponse);

        var sut = CreateSut();

        var first = await sut.GetAsync(
            query,
            CancellationToken.None);

        var second = await sut.GetAsync(
            query,
            CancellationToken.None);

        Assert.Same(first, second);

        clientMock.Verify(
            client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_ConcurrentRequestsForSameKey_OnlyQueriesSourceOnce()
    {
        var query = CreateQuery(
            city: "Denver",
            page: 1,
            pageSize: 20);

        var response = CreatePagedResponse(
            query,
            [
                BreweryTestData.CreateResponse(
                    id: "1",
                    name: "Alpha Brewing",
                    city: "Denver")
            ]);

        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        clientMock
            .Setup(client => client.QueryAsync(
                query,
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await release.Task;
                return response;
            });

        var sut = CreateSut();

        var firstTask = sut.GetAsync(query, CancellationToken.None);
        var secondTask = sut.GetAsync(query, CancellationToken.None);

        await Task.Yield();
        release.SetResult(null);

        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, results.Length);
        Assert.Same(response, results[0]);
        Assert.Same(response, results[1]);
        Assert.Equal(1, Volatile.Read(ref callCount));
    }

    [Fact]
    public async Task GetAsync_DifferentQueries_UseDifferentCacheEntries()
    {
        var denverQuery = CreateQuery(city: "Denver");
        var austinQuery = CreateQuery(city: "Austin");

        var denverResponse = CreatePagedResponse(
            denverQuery,
            [BreweryTestData.CreateResponse(
                id: "1",
                name: "Denver Brewery",
                city: "Denver")]);

        var austinResponse = CreatePagedResponse(
            austinQuery,
            [BreweryTestData.CreateResponse(
                id: "2",
                name: "Austin Brewery",
                city: "Austin")]);

        clientMock
            .Setup(client => client.QueryAsync(
                denverQuery,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(denverResponse);

        clientMock
            .Setup(client => client.QueryAsync(
                austinQuery,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(austinResponse);

        var sut = CreateSut();

        var denverResult = await sut.GetAsync(
            denverQuery,
            CancellationToken.None);

        var austinResult = await sut.GetAsync(
            austinQuery,
            CancellationToken.None);

        Assert.Same(denverResponse, denverResult);
        Assert.Same(austinResponse, austinResult);

        Assert.True(
            cache.Contains(BreweryCacheKey.Create(denverQuery)));

        Assert.True(
            cache.Contains(BreweryCacheKey.Create(austinQuery)));

        Assert.NotEqual(
            BreweryCacheKey.Create(denverQuery),
            BreweryCacheKey.Create(austinQuery));
    }

    [Fact]
    public async Task AutocompleteAsync_CacheMiss_QueriesSourceAndCachesSuggestions()
    {
        const string term = "brew";
        const int limit = 10;

        IReadOnlyList<string> externalSuggestions =
        [
            "Brew House",
            "Brew Works"
        ];

        clientMock
            .Setup(client => client.AutocompleteAsync(
                term,
                limit,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalSuggestions);

        var result = await CreateSut().AutocompleteAsync(
            term,
            limit,
            CancellationToken.None);

        Assert.Same(externalSuggestions, result);

        clientMock.Verify(
            client => client.AutocompleteAsync(
                term,
                limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        var found = cache.TryGet<IReadOnlyList<string>>(
            BreweryCacheKey.CreateAutocomplete(term, limit),
            out var cachedSuggestions);

        Assert.True(found);
        Assert.NotNull(cachedSuggestions);
        Assert.Equal(externalSuggestions, cachedSuggestions);
    }

    [Fact]
    public async Task AutocompleteAsync_CacheHit_DoesNotCallSource()
    {
        const string term = "brew";
        const int limit = 10;
        IReadOnlyList<string> cachedSuggestions = ["Brew House"];
        cache.Seed(BreweryCacheKey.CreateAutocomplete(term, limit), cachedSuggestions);

        var result = await CreateSut().AutocompleteAsync(term, limit, CancellationToken.None);

        Assert.Same(cachedSuggestions, result);
        clientMock.Verify(
            client => client.AutocompleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_SourceFailure_DoesNotCacheAndAllowsRetry()
    {
        var query = CreateQuery(city: "Denver");
        var response = CreatePagedResponse(
            query,
            [BreweryTestData.CreateResponse(name: "Denver Brewery", city: "Denver")]);
        var callCount = 0;

        clientMock
            .Setup(client => client.QueryAsync(query, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    return Task.FromException<PagedResponse<BreweryResponse>>(
                        new HttpRequestException("temporary failure"));
                }

                return Task.FromResult(response);
            });

        var sut = CreateSut();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.GetAsync(query, CancellationToken.None));
        var result = await sut.GetAsync(query, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(2, callCount);
        Assert.True(cache.Contains(BreweryCacheKey.Create(query)));
    }

    [Fact]
    public async Task GetAsync_NullSourceResult_IsNotCached()
    {
        var query = CreateQuery(city: "Denver");
        clientMock
            .Setup(client => client.QueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<BreweryResponse>)null!);

        var result = await CreateSut().GetAsync(query, CancellationToken.None);

        Assert.Null(result);
        Assert.False(cache.Contains(BreweryCacheKey.Create(query)));
    }

    [Fact]
    public async Task GetAsync_CacheFilledBetweenInitialChecks_ReturnsNewValue()
    {
        var query = CreateQuery(city: "Denver");
        var expected = CreatePagedResponse(
            query,
            [BreweryTestData.CreateResponse(name: "Concurrent Brewery", city: "Denver")]);
        var changingCache = new CacheHitOnSecondLookup(expected);
        var sut = new BreweryService(changingCache, clientMock.Object, loggerMock.Object);

        var result = await sut.GetAsync(query, CancellationToken.None);

        Assert.Same(expected, result);
        clientMock.Verify(
            client => client.QueryAsync(It.IsAny<BreweryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
    GetAsync_WhenSourceDoesNotProvideTotal_ReturnsUnknownTotal()
    {
        // Arrange
        var query =
            new BreweryQuery
            {
                SortBy = "distance",
                SortDirection = "asc",
                Latitude = 32.7157m,
                Longitude = -117.1611m,
                Page = 1,
                PageSize = 5
            };

        var sourceResponse =
            new PagedResponse<BreweryResponse>(
                [
                    BreweryTestData.CreateResponse(
                    id: "1",
                    name: "Alpha Brewing",
                    city: "San Diego"),

                BreweryTestData.CreateResponse(
                    id: "2",
                    name: "Beta Brewing",
                    city: "San Diego")
                ],
                query.Page,
                query.PageSize,
                null);

        clientMock
            .Setup(client =>
                client.QueryAsync(
                    query,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceResponse);

        var sut =
            CreateSut();

        // Act
        var result =
            await sut.GetAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.NotNull(result);

        Assert.Equal(
            2,
            result.Breweries.Count);

        Assert.Null(
            result.TotalCount);

        Assert.Equal(
            query.Page,
            result.Page);

        Assert.Equal(
            query.PageSize,
            result.PageSize);

        clientMock.Verify(
            client =>
                client.QueryAsync(
                    query,
                    It.IsAny<CancellationToken>()),
            Times.Once);

        var cacheKey =
            BreweryCacheKey.Create(
                query);

        var found =
            cache.TryGet<
                PagedResponse<BreweryResponse>>(
                cacheKey,
                out var cachedResponse);

        Assert.True(found);
        Assert.NotNull(cachedResponse);

        Assert.Null(
            cachedResponse.TotalCount);

        Assert.Same(
            sourceResponse,
            cachedResponse);
    }

    private BreweryService CreateSut()
    {
        return new BreweryService(
            cache,
            clientMock.Object,
            loggerMock.Object);
    }

    private static BreweryQuery CreateQuery(
        string? search = null,
        string? name = null,
        string? city = null,
        string? sortBy = null,
        string sortDirection = "asc",
        decimal? latitude = null,
        decimal? longitude = null,
        int page = 1,
        int pageSize = 50)
    {
        return new BreweryQuery
        {
            Search = search,
            Name = name,
            City = city,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Latitude = latitude,
            Longitude = longitude,
            Page = page,
            PageSize = pageSize
        };
    }

    private static PagedResponse<BreweryResponse> CreatePagedResponse(
        BreweryQuery query,
        IReadOnlyList<BreweryResponse> breweries,
        int? totalCount = null)
    {
        return new PagedResponse<BreweryResponse>(
            breweries,
            query.Page,
            query.PageSize,
            totalCount ?? breweries.Count);
    }

    private sealed class TestBreweryCache : IBreweryCache
    {
        private readonly Dictionary<string, object> values =
            new(StringComparer.Ordinal);

        public bool TryGet<T>(
            string key,
            out T? value)
        {
            if (values.TryGetValue(key, out var storedValue) &&
                storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(
            string key,
            T value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);

            values[key] = value;
        }

        public void Seed<T>(
            string key,
            T value)
        {
            Set(key, value);
        }

        public bool Contains(
            string key)
        {
            return values.ContainsKey(key);
        }
    }

    private sealed class CacheHitOnSecondLookup(PagedResponse<BreweryResponse> response) : IBreweryCache
    {
        private int lookupCount;

        public bool TryGet<T>(string key, out T? value)
        {
            if (Interlocked.Increment(ref lookupCount) == 2 && response is T typedResponse)
            {
                value = typedResponse;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            throw new InvalidOperationException("The cached value should avoid a source call.");
        }
    }
}
