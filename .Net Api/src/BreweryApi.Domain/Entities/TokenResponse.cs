namespace BreweryApi.Domain.Entities;

/// <summary>Opaque access token and its expiration timestamp.</summary>
/// <param name="AccessToken">Cryptographically random access token.</param>
/// <param name="RefreshToken">Single-use token used to obtain a new access token.</param>
/// <param name="TokenType">Header name used to present the access token.</param>
/// <param name="ExpiresAt">UTC access-token expiration time.</param>
/// <param name="RefreshExpiresAt">UTC refresh-token expiration time.</param>
/// <param name="CanUseAutocomplete">Whether the account can use brewery autocomplete.</param>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshExpiresAt,
    bool CanUseAutocomplete);
