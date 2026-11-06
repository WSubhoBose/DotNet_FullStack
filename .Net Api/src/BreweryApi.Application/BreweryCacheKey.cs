using System;
using System.Globalization;
using BreweryApi.Domain.Models;

namespace BreweryApi.Application;

public static class BreweryCacheKey
{
    public static string Create(BreweryQuery query)
    {
        return string.Join(
            ":",
            "breweries",
            Normalise(query.Search),
            Normalise(query.Name),
            Normalise(query.City),
            Normalise(query.SortBy),
            Normalise(query.SortDirection),
            Coordinate(query.Latitude),
            Coordinate(query.Longitude),
            query.Page,
            query.PageSize);
    }

    private static string Normalise(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().ToLowerInvariant();
    }

    private static string Coordinate(decimal? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "_";
    }

    public static string CreateAutocomplete(string term, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        return string.Join(
            ":",
            "breweries",
            "autocomplete",
            term.Trim().ToLowerInvariant(),
            limit);
    }
}
