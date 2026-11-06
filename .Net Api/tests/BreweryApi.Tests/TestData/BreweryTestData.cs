using BreweryApi.Domain.Models;

namespace BreweryApi.Tests.TestData;

public static class BreweryTestData
{
    public static BreweryResponse CreateResponse(
        string id = "1",
        string name = "Alpha Brewing",
        string city = "Denver",
        string breweryType = "micro",
        string? phone = "1234567890",
        string stateProvince = "Colorado",
        string postalCode = "80201",
        string country = "United States",
        decimal? longitude = -104.9903m,
        decimal? latitude = 39.7392m,
        string? address1 = "1 Main Street",
        string? address2 = null,
        string? address3 = null,
        string? websiteUrl = "https://example.test",
        string? state = "Colorado",
        string? street = "1 Main Street")
    {
        return new BreweryResponse(
            Id: id,
            Name: name,
            BreweryType: breweryType,
            Address1: address1,
            Address2: address2,
            Address3: address3,
            City: city,
            StateProvince: stateProvince,
            PostalCode: postalCode,
            Country: country,
            Longitude: longitude,
            Latitude: latitude,
            Phone: phone,
            WebsiteUrl: websiteUrl,
            State: state,
            Street: street);
    }

    public static IReadOnlyList<BreweryResponse>
        CreateCatalogue()
    {
        return
        [
            CreateResponse(
                id: "1",
                name: "Alpha Brewing",
                city: "Denver"),

            CreateResponse(
                id: "2",
                name: "Beta Brewery",
                city: "Austin",
                stateProvince: "Texas"),

            CreateResponse(
                id: "3",
                name: "Central Brewing",
                city: "Denver"),

            CreateResponse(
                id: "4",
                name: "Delta Brewery",
                city: "Boston",
                stateProvince: "Massachusetts"),

            CreateResponse(
                id: "5",
                name: "Alpha Craft Brewery",
                city: "Austin",
                stateProvince: "Texas")
        ];
    }
}