namespace BreweryApi.Domain.Models
{
    /// <summary>Public brewery details returned by the brewery API.</summary>
    /// <param name="Id">Unique brewery identifier.</param>
    /// <param name="Name">Brewery name.</param>
    /// <param name="BreweryType">Brewery classification.</param>
    /// <param name="Address1">First address line, if available.</param>
    /// <param name="Address2">Second address line, if available.</param>
    /// <param name="Address3">Third address line, if available.</param>
    /// <param name="City">City where the brewery is located.</param>
    /// <param name="StateProvince">State or province where the brewery is located.</param>
    /// <param name="PostalCode">Postal code, if available.</param>
    /// <param name="Country">Country where the brewery is located.</param>
    /// <param name="Longitude">Longitude, if available.</param>
    /// <param name="Latitude">Latitude, if available.</param>
    /// <param name="Phone">Contact phone number, if available.</param>
    /// <param name="WebsiteUrl">Website URL, if available.</param>
    /// <param name="State">State name, if available.</param>
    /// <param name="Street">Street address, if available.</param>
    /// <param name="Distance">Distance from the requested coordinates when sorting by distance.</param>
    public sealed record BreweryResponse(
    string Id,
    string Name,
    string BreweryType,
    string? Address1,
    string? Address2,
    string? Address3,
    string City,
    string StateProvince,
    string PostalCode,
    string Country,
    decimal? Longitude,
    decimal? Latitude,
    string? Phone,
    string? WebsiteUrl,
    string? State,
    string? Street,
    string? Distance = null);
}
