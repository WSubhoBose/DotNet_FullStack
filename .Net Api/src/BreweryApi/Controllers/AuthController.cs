using Asp.Versioning;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BreweryApi.Controllers;

/// <summary>Issues access tokens for the brewery UI.</summary>
[ApiController]
[ApiVersionNeutral]
[Route("api/auth")]
public sealed class AuthController(ITokenService tokenService) : ControllerBase
{
    /// <summary>Checks that the username is in SQLite and returns an access token.</summary>
    /// <param name="request">Username and password to validate.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>An opaque access token and its expiration timestamp.</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("TokenIssuePolicy")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> CreateToken(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.CreateAsync(request, cancellationToken);
        return token is null
            ? Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Username not found",
                Detail = "This username is not registered. Use a username from the Users table."
            })
            : Ok(token);
    }

    /// <summary>Rotates a refresh token and returns a new access and refresh token pair.</summary>
    /// <param name="request">Refresh token to rotate.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>A new token pair, or an unauthorized response for an invalid refresh token.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("TokenIssuePolicy")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.RefreshAsync(request.RefreshToken, cancellationToken);
        return token is null
            ? Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh failed",
                Detail = "This refresh token is not valid or has already been used."
            })
            : Ok(token);
    }
}
