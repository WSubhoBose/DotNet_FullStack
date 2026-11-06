using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using BreweryApi.Domain;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryApi.Controllers;

/// <summary>Provides authenticated version 2 brewery search and autocomplete endpoints.</summary>
[ApiController]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/breweries")]
[Authorize]
public sealed class BreweriesV2Controller(IBreweryCatalogService breweryCatalogService) : ControllerBase
{
    /// <summary>Searches the SQLite brewery catalog with filtering, sorting, and pagination.</summary>
    /// <param name="query">Filters, sort options, coordinates, and pagination settings.</param>
    /// <param name="ct">Token used to cancel the request when the client disconnects.</param>
    /// <returns>A page of matching breweries and pagination metadata.</returns>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PagedResponse<BreweryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<PagedResponse<BreweryResponse>> Get([FromQuery] BreweryQuery query, CancellationToken ct) =>
        breweryCatalogService.GetAsync(query, ct);

    /// <summary>Returns autocomplete suggestions from the SQLite brewery catalog.</summary>
    /// <param name="term">Required text used to find matching brewery values.</param>
    /// <param name="ct">Token used to cancel the request when the client disconnects.</param>
    /// <param name="limit">Maximum number of suggestions to return; defaults to 10.</param>
    /// <returns>A list of matching brewery suggestions.</returns>
    [HttpGet("autocomplete")]
    [Authorize(Policy = "AutocompleteAccess")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<string>> Autocomplete(
        [FromQuery, Required] string term,
        CancellationToken ct,
        [FromQuery] int limit = 10) =>
        breweryCatalogService.AutocompleteAsync(term, limit, ct);
}
