using System.ComponentModel.DataAnnotations;

namespace BreweryApi.Domain.Configurations;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10000)]
    public int PermitLimit { get; set; } = 60;

    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    [Range(0, 1000)]
    public int QueueLimit { get; set; }
}