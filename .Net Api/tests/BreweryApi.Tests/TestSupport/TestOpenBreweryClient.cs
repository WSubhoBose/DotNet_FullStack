using BreweryApi.Domain;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Tests.TestData;

namespace BreweryApi.Tests.TestSupport;

public sealed class TestOpenBreweryClient : IOpenBreweryClient
{
    private static readonly IReadOnlyList<BreweryResponse>
        Breweries =
        [
            BreweryTestData.CreateResponse(
                id: "test-1",
                name: "Alpha Brewing",
                city: "Denver"),

            BreweryTestData.CreateResponse(
                id: "test-2",
                name: "Beta Brewery",
                city: "Austin"),

            BreweryTestData.CreateResponse(
                id: "test-3",
                name: "Central Brewing",
                city: "San Diego")
        ];

    public Task<PagedResponse<BreweryResponse>> QueryAsync(
        BreweryQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<BreweryResponse> result =
            Breweries;

        if (!string.IsNullOrWhiteSpace(
                query.Search))
        {
            result =
                result.Where(
                    brewery =>
                        brewery.Name.Contains(
                            query.Search.Trim(),
                            StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(
                query.Name))
        {
            result =
                result.Where(
                    brewery =>
                        brewery.Name.Contains(
                            query.Name.Trim(),
                            StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(
                query.City))
        {
            result =
                result.Where(
                    brewery =>
                        brewery.City.Contains(
                            query.City.Trim(),
                            StringComparison.OrdinalIgnoreCase));
        }

        result =
            ApplySorting(
                result,
                query);

        var items =
            result
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToList();

        var response =
            new PagedResponse<BreweryResponse>(
                items,
                query.Page,
                query.PageSize,
                null);

        return Task.FromResult(
            response);
    }

    public Task<IReadOnlyList<string>>
    AutocompleteAsync(
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var normalisedTerm =
            term.Trim();

        IReadOnlyList<string> suggestions =
            Breweries
                .SelectMany(
                    GetSuggestionValues)
                .Where(
                    value =>
                        value.Contains(
                            normalisedTerm,
                            StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    value => value,
                    StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();

        return Task.FromResult(
            suggestions);
    }

    private static IEnumerable<BreweryResponse>
        ApplySorting(
            IEnumerable<BreweryResponse> breweries,
            BreweryQuery query)
    {
        var descending =
            string.Equals(
                query.SortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase);

        return query.SortBy?
            .Trim()
            .ToLowerInvariant()
            switch
        {
            "name" when descending =>
                breweries.OrderByDescending(
                    brewery =>
                        brewery.Name),

            "name" =>
                breweries.OrderBy(
                    brewery =>
                        brewery.Name),

            "city" when descending =>
                breweries.OrderByDescending(
                    brewery =>
                        brewery.City),

            "city" =>
                breweries.OrderBy(
                    brewery =>
                        brewery.City),

            _ =>
                breweries
        };
    }

    private static IEnumerable<string> GetSuggestionValues(BreweryResponse brewery)
    {
        if (!string.IsNullOrWhiteSpace(
                brewery.Name))
        {
            yield return brewery.Name;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.City))
        {
            yield return brewery.City;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.StateProvince))
        {
            yield return brewery.StateProvince;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.Country))
        {
            yield return brewery.Country;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.PostalCode))
        {
            yield return brewery.PostalCode;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.BreweryType))
        {
            yield return brewery.BreweryType;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.Phone))
        {
            yield return brewery.Phone;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.State))
        {
            yield return brewery.State;
        }

        if (!string.IsNullOrWhiteSpace(
                brewery.Street))
        {
            yield return brewery.Street;
        }
    }
}