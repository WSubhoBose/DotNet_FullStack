using System.ComponentModel.DataAnnotations;

namespace BreweryApi.Domain.Entities;

/// <summary>A username and string value submitted to request a short-lived access token.</summary>
public sealed record TokenRequest
{
    /// <summary>Username of an account stored in the SQLite Users table.</summary>
    [Required, MaxLength(200)]
    public required string Username { get; init; }

    /// <summary>Request field that is not checked or stored by the API.</summary>
    public required string Password { get; init; }
}
