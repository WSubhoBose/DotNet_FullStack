using BreweryApi.Domain.Entities;

namespace BreweryApi.Domain.Interfaces;

/// <summary>Checks stored usernames and creates persisted access tokens.</summary>
public interface ITokenService
{
    /// <summary>Creates a token when the request credentials are valid.</summary>
    /// <param name="request">Credentials to validate.</param>
    /// <param name="cancellationToken">Token used to cancel database work.</param>
    /// <returns>The issued token, or null when the credentials are invalid.</returns>
    Task<TokenResponse?> CreateAsync(TokenRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates a valid refresh token and creates a new token pair.</summary>
    /// <param name="refreshToken">Single-use refresh token to rotate.</param>
    /// <param name="cancellationToken">Token used to cancel database work.</param>
    /// <returns>The new token pair, or null when the refresh token is invalid or expired.</returns>
    Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
