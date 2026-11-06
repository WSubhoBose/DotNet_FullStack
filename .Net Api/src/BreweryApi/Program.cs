using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Extensions;
using BreweryApi.Middleware;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using BreweryApi.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(
(context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "BreweryApi");
});
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services
    .AddAuthentication("Auth")
    .AddScheme<AuthenticationSchemeOptions, BreweryApi.Authentication.CustomTokenAuthenticationHandler>("Auth", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AutocompleteAccess", policy =>
        policy.RequireClaim("permission", "autocomplete"));
});
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
    options.ReportApiVersions = true;
}).AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddDependencies(builder.Configuration);
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Brewery API", Version = "v1" });
    options.SwaggerDoc("v2", new OpenApiInfo { Title = "Brewery API", Version = "v2" });
    options.AddSecurityDefinition("Auth", new OpenApiSecurityScheme
    {
        Name = "Auth",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header
    });
    options.OperationFilter<AuthorizationOperationFilter>();
    options.DocInclusionPredicate((documentName, apiDescription) => apiDescription.GroupName == documentName);
    options.IncludeXmlComments(Path.ChangeExtension(typeof(Program).Assembly.Location, ".xml"));
    options.IncludeXmlComments(Path.ChangeExtension(typeof(BreweryQuery).Assembly.Location, ".xml"));
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider
        .GetRequiredService<IActiveTokenStore>()
        .InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(
    options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} " +
            "responded {StatusCode} in {Elapsed:0.0000} ms";

        options.GetLevel =
            (
                httpContext,
                elapsed,
                exception) =>
            {
                if (exception is not null ||
                    httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (httpContext.Response.StatusCode >= 400)
                {
                    return LogEventLevel.Warning;
                }

                return LogEventLevel.Information;
            };

        options.EnrichDiagnosticContext =
            (
                diagnosticContext,
                httpContext) =>
            {
                diagnosticContext.Set(
                    "RequestHost",
                    httpContext.Request.Host.Value
                    ?? string.Empty);

                diagnosticContext.Set(
                    "RequestScheme",
                    httpContext.Request.Scheme
                    ?? string.Empty);

                diagnosticContext.Set(
                    "TraceId",
                    httpContext.TraceIdentifier
                    ?? string.Empty);
            };
    });
app.UseRateLimiter();
app.UseSwagger();
var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
app.UseSwaggerUI(options =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions.Reverse())
    {
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"Brewery API {description.GroupName}");
    }

    options.RoutePrefix = "swagger";
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("BreweryApiPolicy");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Entry point for the Brewery API application.</summary>
public partial class Program { };
