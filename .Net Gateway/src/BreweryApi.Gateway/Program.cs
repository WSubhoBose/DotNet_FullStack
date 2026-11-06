using BreweryApi.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using MMLib.SwaggerForOcelot;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Ui", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services
    .AddAuthentication("Auth")
    .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>("Auth", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerForOcelot(builder.Configuration, options =>
    options.GenerateDocsForGatewayItSelf = false);

var app = builder.Build();
app.UseExceptionHandler();
app.UseRouting();
app.UseCors("Ui");
app.UseAuthentication();
app.UseAuthorization();
app.UseSwaggerForOcelotUI(options =>
{
    options.PathToSwaggerGenerator = "/swagger/docs";
});
await app.UseOcelot();
app.Run();

/// <summary>Entry point for the Brewery Gateway application.</summary>
public partial class Program { }
