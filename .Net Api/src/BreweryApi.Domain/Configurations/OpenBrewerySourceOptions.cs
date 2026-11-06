using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BreweryApi.Domain.Configurations
{
    public sealed class OpenBrewerySourceOptions
    {
        public const string SectionName = "OpenBrewerySource";

        [Required]
        [Url]
        public string BaseUrl { get; init; } = string.Empty;

        [Range(1, 300)]
        public int TimeoutSeconds { get; init; } = 30;

        [Range(0, 10)]
        public int RetryCount { get; init; } = 3;

        [Range(0, 60)]
        public int RetryDelaySeconds { get; init; } = 2;

        [Required]
        [ValidateObjectMembers]
        public CircuitBreakerOptions CircuitBreaker { get; init; } = new();
    }
}