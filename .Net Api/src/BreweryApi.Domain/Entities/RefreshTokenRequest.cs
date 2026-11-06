using System.ComponentModel.DataAnnotations;

namespace BreweryApi.Domain.Entities;

/// <summary>Single-use refresh token submitted to obtain a new token pair.</summary>
public sealed record RefreshTokenRequest
{
    /// <summary>Refresh token returned when the access token was issued.</summary>
    [Required, MaxLength(200)]
    public required string RefreshToken { get; init; }
}
