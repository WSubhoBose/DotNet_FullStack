using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BreweryApi.Tests.Integration;

public sealed class BreweryApiIntegrationTests(BreweryApiFactory factory) : IClassFixture<BreweryApiFactory>
{
    [Fact]
    public async Task GetBreweries_ReturnsSuccess()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?page=1&pageSize=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task V1SwaggerOperations_AreMarkedDeprecated()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var securityScheme = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Auth");
        Assert.Equal("apiKey", securityScheme.GetProperty("type").GetString());
        Assert.Equal("Auth", securityScheme.GetProperty("name").GetString());
        Assert.Equal("header", securityScheme.GetProperty("in").GetString());
        using var v2Response = await client.GetAsync("/swagger/v2/swagger.json");
        using var v2Document = JsonDocument.Parse(await v2Response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, v2Response.StatusCode);
        var v2BreweryGet = v2Document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v2/breweries")
            .GetProperty("get");
        Assert.True(v2BreweryGet
            .GetProperty("security")[0]
            .TryGetProperty("Auth", out _));
        var v1AutocompleteGet = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/breweries/autocomplete")
            .GetProperty("get");
        Assert.False(v1AutocompleteGet.TryGetProperty("security", out _));
        Assert.True(document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/breweries")
            .GetProperty("get")
            .GetProperty("deprecated")
            .GetBoolean());
        Assert.True(document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/breweries/autocomplete")
            .GetProperty("get")
            .GetProperty("deprecated")
            .GetBoolean());
    }

    [Fact]
    public async Task GetBreweriesV2_RequiresAuthHeaderToken()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v2/breweries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBreweriesV2_RejectsUnknownAuthHeaderToken()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/breweries");
        request.Headers.Add("Auth", "unknown-token");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBreweriesV2_UsesPersistedCustomTokenAndSqliteCatalog()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var token = client.DefaultRequestHeaders.GetValues("Auth").Single();
        var tokenStore = factory.Services.GetRequiredService<BreweryApi.Domain.Interfaces.IActiveTokenStore>();
        Assert.True(await tokenStore.IsActiveAsync(token, CancellationToken.None));

        using var response = await client.GetAsync("/api/v2/breweries?page=1&pageSize=10");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        Assert.Contains("Alpha Brewing", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DistanceWithoutCoordinates_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?sortBy=distance");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task DistanceWithoutCoordinates_ReturnsFriendlyMessage()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?sortBy=distance");

        var content =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "Latitude and longitude are required",
            content);
    }

    [Theory]
    [InlineData("name", "asc")]
    [InlineData("name", "desc")]
    [InlineData("city", "asc")]
    [InlineData("city", "desc")]
    public async Task Sorting_ReturnsSuccess(
        string sortBy,
        string direction)
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                $"/api/v1/breweries" +
                $"?sortBy={sortBy}" +
                $"&sortDirection={direction}" +
                "&page=1&pageSize=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Autocomplete_ReturnsSuccess()
    {
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries/autocomplete" +
                "?term=brew&limit=10");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/breweries/autocomplete?term=brew", HttpStatusCode.OK)]
    [InlineData("/api/v2/breweries/autocomplete?term=brew", HttpStatusCode.Forbidden)]
    public async Task Autocomplete_RespectsVersionAndUserPermissions(string requestUri, HttpStatusCode expectedStatus)
    {
        using var client = await factory.CreateAuthenticatedClientAsync("user2");

        using var response = await client.GetAsync(requestUri);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Your account cannot use the autocomplete API.", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InvalidPage_ReturnsBadRequest()
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/api/v1/breweries?page=0");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsSuccess()
    {
        using var client =
            factory.CreateClient();

        using var response =
            await client.GetAsync(
                "/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task
    Breweries_WhenMoreThan60RequestsAreMade_Returns429()
    {
        // Arrange
        await using var factory =
            new RateLimitedBreweryApiFactory();

        using var client =
            factory.CreateClient();

        const string requestUri =
            "/api/v1/breweries" +
            "?page=1&pageSize=10";

        // Act + Assert
        for (var requestNumber = 1; requestNumber <= 60; requestNumber++)
        {
            using var response = await client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // The 61st request exceeds the configured limit.
        using var rejectedResponse =
            await client.GetAsync(
                requestUri);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejectedResponse.StatusCode);

        Assert.Equal(
            "60",
            rejectedResponse.Headers
                .GetValues("Retry-After")
                .Single());

        Assert.Equal("application/json", rejectedResponse.Content.Headers.ContentType?.MediaType);

        Assert.True(rejectedResponse.Headers.Contains("Retry-After"));

        var responseBody = await rejectedResponse.Content.ReadAsStringAsync();

        Assert.Contains(
            "Too many requests",
            responseBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_IsNotSubjectToBreweryRateLimit()
    {
        using var client = factory.CreateClient();

        for (var index = 0; index < 5; index++)
        {
            using var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }


}