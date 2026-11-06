using System.ComponentModel.DataAnnotations;

namespace BreweryApi.Domain.Configurations
{
    public sealed class CacheOptions
    {
        public const string SectionName = "Cache";

        [Range(1, 1440)]
        public int BreweryExpirationMinutes { get; init; } = 10;
    }
}