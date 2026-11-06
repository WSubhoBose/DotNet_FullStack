using BreweryApi.Domain.Configurations;
using Microsoft.Extensions.Options;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class OpenBrewerySourceOptionsTests
{
    [Fact]
    public void Validate_RejectsInvalidRetryAndCircuitBreakerSettings()
    {
        var options = new OpenBrewerySourceOptions
        {
            BaseUrl = "https://api.openbrewerydb.org/v1/breweries",
            RetryCount = 11,
            CircuitBreaker = new CircuitBreakerOptions
            {
                FailureRatio = 1.5
            }
        };
        var validator = new DataAnnotationValidateOptions<OpenBrewerySourceOptions>(
            Options.DefaultName);

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains(nameof(options.RetryCount)));
        Assert.Contains(result.Failures!, failure => failure.Contains(nameof(options.CircuitBreaker)));
    }
}
