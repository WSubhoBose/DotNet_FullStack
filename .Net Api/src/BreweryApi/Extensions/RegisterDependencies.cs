using BreweryApi.Application.Service;
using BreweryApi.Domain.Configurations;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Helpers;
using BreweryApi.Infrastructure;
using BreweryApi.Infrastructure.Persistence;
using BreweryApi.Application.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using System.Globalization;
using System.Threading.RateLimiting;

namespace BreweryApi.Extensions
{
    /// <summary>Registers the brewery API's application and infrastructure dependencies.</summary>
    public static class RegisterDependencies
    {
        /// <summary>
        /// Registers persistence and external-service infrastructure.
        /// </summary>
        public static IServiceCollection AddDependencies(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContextFactory<BreweryDbContext>((serviceProvider, options) =>
            {
                var appConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
                var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var path = GetDatabasePath(
                    appConfiguration["Data:SqlitePath"] ?? "data/brewery.db",
                    hostEnvironment.ContentRootPath);
                options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            });
            services.AddDbContextFactory<AuthDbContext>((serviceProvider, options) =>
            {
                var appConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
                var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
                var path = GetDatabasePath(
                    appConfiguration["Auth:DatabasePath"] ?? "../../data/auth.db",
                    hostEnvironment.ContentRootPath);
                options.UseSqlite(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            });

            services.AddOptions<OpenBrewerySourceOptions>()
                    .Bind(configuration.GetSection(OpenBrewerySourceOptions.SectionName))
                    .ValidateDataAnnotations()
                    .Validate(
                        options =>
                            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                        "OpenBrewerySource:BaseUrl must be an absolute HTTP or HTTPS URL.")
                    .ValidateOnStart();

            services.AddOptions<CacheOptions>()
                    .Bind(configuration.GetSection(CacheOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            services.AddOptions<RateLimitOptions>()
                    .Bind(configuration.GetSection(RateLimitOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName)
                    .Get<RateLimitOptions>()
                    ?? throw new InvalidOperationException("Rate-limiting configuration is missing.");

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter(
                    policyName: "BreweryApiPolicy",
                    limiterOptions =>
                    {
                        limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
                        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
                        limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
                        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                        limiterOptions.AutoReplenishment = true;
                    });

                options.AddFixedWindowLimiter(
                    policyName: "TokenIssuePolicy",
                    limiterOptions =>
                    {
                        limiterOptions.PermitLimit = 10;
                        limiterOptions.Window = TimeSpan.FromMinutes(1);
                        limiterOptions.QueueLimit = 0;
                        limiterOptions.AutoReplenishment = true;
                    });

                options.OnRejected =
                    async (context, cancellationToken) =>
                    {
                        var httpContext = context.HttpContext;
                        var response = httpContext.Response;
                        response.StatusCode = StatusCodes.Status429TooManyRequests;
                        var isTokenIssuePolicy = httpContext.GetEndpoint()?
                            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?
                            .PolicyName == "TokenIssuePolicy";
                        var permitLimit = isTokenIssuePolicy ? 10 : rateLimitOptions.PermitLimit;

                        response.Headers.RetryAfter =
                            rateLimitOptions
                                .WindowSeconds
                                .ToString(CultureInfo.InvariantCulture);

                        var problemDetails =
                            new ProblemDetails
                            {
                                Status = StatusCodes.Status429TooManyRequests,
                                Title = "Too many requests",

                                Detail =
                                    $"The API request limit of " +
                                    $"{permitLimit} requests " +
                                    $"per {rateLimitOptions.WindowSeconds} seconds " +
                                    "has been exceeded.",

                                Instance = httpContext.Request.Path
                            };

                        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

                        await response.WriteAsJsonAsync(problemDetails,
                            cancellationToken: cancellationToken);
                    };
            });

            services.AddHealthChecks();

            var openBrewerySettings = configuration.GetSection(OpenBrewerySourceOptions.SectionName)
                            .Get<OpenBrewerySourceOptions>()
                            ?? throw new InvalidOperationException("OpenBrewery Source configuration is missing.");

            services.AddHttpClient<IOpenBreweryClient, OpenBreweryClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OpenBrewerySourceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            }).AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = openBrewerySettings.RetryCount;
                options.Retry.Delay = TimeSpan.FromSeconds(openBrewerySettings.RetryDelaySeconds);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = openBrewerySettings.CircuitBreaker.FailureRatio;
                options.CircuitBreaker.MinimumThroughput = openBrewerySettings.CircuitBreaker.MinimumThroughput;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(openBrewerySettings.CircuitBreaker.SamplingDurationSeconds);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(openBrewerySettings.CircuitBreaker.BreakDurationSeconds);
            });

            services.AddScoped<IBreweryMapper, BreweryMapper>();
            services.AddSingleton<IDistanceCalculator, DistanceCalculator>();
            services.AddMemoryCache();
            services.AddSingleton<IBreweryCache, BreweryMemoryCache>();
            services.AddScoped<IBreweryService, BreweryService>();
            services.AddScoped<IBreweryCatalogService, SqliteBreweryCatalogService>();
            services.AddSingleton<IActiveTokenStore, SqliteActiveTokenStore>();
            services.AddScoped<ITokenService, TokenService>();

            return services;
        }

        private static string GetDatabasePath(string configuredPath, string contentRootPath)
        {
            var path = Path.GetFullPath(configuredPath, contentRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
    }
}
