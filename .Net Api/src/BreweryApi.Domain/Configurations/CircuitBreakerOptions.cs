using System.ComponentModel.DataAnnotations;

namespace BreweryApi.Domain.Configurations
{
    public sealed class CircuitBreakerOptions
    {
        [Range(0.01, 1.0)]
        public double FailureRatio { get; init; } = 0.5;

        [Range(2, 1000)]
        public int MinimumThroughput { get; init; } = 5;

        [Range(1, 600)]
        public int SamplingDurationSeconds { get; init; } = 30;

        [Range(1, 600)]
        public int BreakDurationSeconds { get; init; } = 30;
    }
}
