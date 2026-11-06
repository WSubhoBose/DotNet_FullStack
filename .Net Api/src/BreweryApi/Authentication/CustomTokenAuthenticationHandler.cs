using System.Security.Claims;
using System.Text.Encodings.Web;
using BreweryApi.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BreweryApi.Authentication;

internal sealed class CustomTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IActiveTokenStore tokenStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Auth", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        if (headerValues.Count != 1 || string.IsNullOrWhiteSpace(headerValues[0]))
        {
            return AuthenticateResult.Fail("The Auth header must contain one access token.");
        }

        var user = await tokenStore.GetActiveUserAsync(headerValues[0]!, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("The access token is not valid or has expired.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim("permission", user.CanUseAutocomplete ? "autocomplete" : "brewery-search")
            ],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Sign-in required",
            Detail = "Send a valid access token in the Auth header.",
            Instance = Request.Path
        });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Access denied",
            Detail = "Your account cannot use the autocomplete API.",
            Instance = Request.Path
        });
    }
}
