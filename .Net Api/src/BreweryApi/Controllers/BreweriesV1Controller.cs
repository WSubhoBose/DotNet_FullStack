using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using BreweryApi.Domain;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BreweryApi.Controllers
{
    /// <summary>Provides version 1 brewery search and autocomplete endpoints.</summary>
    /// <param name="breweryDataService">Application service for brewery search operations.</param>
    [ApiController]
    [ApiVersion(1, Deprecated = true)]
    [Route("api/v{version:apiVersion}/breweries")]
    [AllowAnonymous]
    public sealed class BreweriesV1Controller(IBreweryService breweryDataService) : ControllerBase
    {
        /// <summary>Searches and lists breweries with filtering, sorting, and pagination.</summary>
        /// <remarks>
        /// Results can be filtered by search text, brewery name, or city. Page size is limited
        /// to 200. Distance sorting requires both latitude and longitude.
        /// </remarks>
        /// <param name="query">Filters, sort options, coordinates, and pagination settings.</param>
        /// <param name="ct">Token used to cancel the request when the client disconnects.</param>
        /// <returns>A page of matching breweries and pagination metadata.</returns>
        [HttpGet]
        [Obsolete("Use API v2 instead.")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PagedResponse<BreweryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
        public Task<PagedResponse<BreweryResponse>> Get([FromQuery] BreweryQuery query, CancellationToken ct) => breweryDataService.GetAsync(query, ct);

        /// <summary>Returns autocomplete suggestions for brewery names matching a term.</summary>
        /// <remarks>The term must contain at least two characters; the limit must be between 1 and 20.</remarks>
        /// <param name="term">Required text used to find matching brewery names.</param>
        /// <param name="ct">Token used to cancel the request when the client disconnects.</param>
        /// <param name="limit">Maximum number of suggestions to return; defaults to 10.</param>
        /// <returns>A list of matching brewery-name suggestions.</returns>
        [HttpGet("autocomplete")]
        [Obsolete("Use API v2 instead.")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
        public Task<IReadOnlyList<string>> Autocomplete([FromQuery, Required] string term, CancellationToken ct, [FromQuery] int limit = 10) => breweryDataService.AutocompleteAsync(term, limit, ct);
    }
}