using BreweryApi.Domain.Models;
using BreweryApi.Domain.Entities;

namespace BreweryApi.Domain
{
    /// <summary>A page of brewery results and its pagination metadata.</summary>
    /// <typeparam name="T">Type of each item in the result page.</typeparam>
    /// <param name="Breweries">Items on the current page.</param>
    /// <param name="Page">One-based current page number.</param>
    /// <param name="PageSize">Maximum number of items per page.</param>
    /// <param name="TotalCount">Total matching item count, when supplied by the data source.</param>
    public sealed record PagedResponse<T>(IReadOnlyList<T> Breweries, int Page, int PageSize, int? TotalCount);

    /// <summary>Maps a query parameter from an external brewery record to a value.</summary>
    /// <param name="QueryParameter">External API query parameter name.</param>
    /// <param name="Selector">Selects the corresponding value from a brewery record.</param>
    public sealed record AutocompleteFilter(string QueryParameter, Func<SourceBrewery, string?> Selector);

    /// <summary>A brewery and its calculated distance from a search coordinate.</summary>
    /// <param name="Brewery">Brewery details.</param>
    /// <param name="DistanceKilometres">Distance in kilometres, or null when coordinates are unavailable.</param>
    public sealed record BreweryWithDistance(BreweryResponse Brewery, double? DistanceKilometres);
}