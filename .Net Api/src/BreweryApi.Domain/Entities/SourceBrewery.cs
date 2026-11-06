using System.Text.Json.Serialization;

namespace BreweryApi.Domain.Entities
{
    public sealed class SourceBrewery
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("brewery_type")]
        public string BreweryType { get; init; } = string.Empty;

        [JsonPropertyName("address_1")]
        public string? Address1 { get; init; }

        [JsonPropertyName("address_2")]
        public string? Address2 { get; init; }

        [JsonPropertyName("address_3")]
        public string? Address3 { get; init; }

        [JsonPropertyName("city")]
        public string City { get; init; } = string.Empty;

        [JsonPropertyName("state_province")]
        public string StateProvince { get; init; } = string.Empty;

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; init; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; init; } = string.Empty;

        [JsonPropertyName("longitude")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Longitude { get; init; }

        [JsonPropertyName("latitude")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Latitude { get; init; }

        [JsonPropertyName("phone")]
        public string? Phone { get; init; }

        [JsonPropertyName("website_url")]
        public string? WebsiteUrl { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("street")]
        public string? Street { get; init; }
    }
}