namespace BreweryApi.Domain.Models
{
    /// <summary>Filtering, sorting, coordinate, and pagination options for a brewery search.</summary>
    /// <param name="Search">Optional search text matched against brewery names.</param>
    /// <param name="Name">Optional brewery-name filter; takes precedence over <paramref name="Search"/>.</param>
    /// <param name="City">Optional city filter.</param>
    /// <param name="SortBy">Optional sort field: <c>name</c>, <c>city</c>, or <c>distance</c>.</param>
    /// <param name="SortDirection">Sort direction, either <c>asc</c> or <c>desc</c>; defaults to <c>asc</c>.</param>
    /// <param name="Page">One-based page number; defaults to 1.</param>
    /// <param name="PageSize">Number of results per page, from 1 to 200; defaults to 50.</param>
    /// <param name="Latitude">Latitude for distance sorting, between -90 and 90.</param>
    /// <param name="Longitude">Longitude for distance sorting, between -180 and 180.</param>
    public sealed record BreweryQuery(
        string? Search = null,
        string? Name = null,
        string? City = null,
        string? SortBy = null,
        string SortDirection = "asc",
        int Page = 1,
        int PageSize = 50,
        decimal? Latitude = null,
        decimal? Longitude = null
    );
}