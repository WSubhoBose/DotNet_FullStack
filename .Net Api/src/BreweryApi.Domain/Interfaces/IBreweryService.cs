using BreweryApi.Domain.Models;

namespace BreweryApi.Domain.Interfaces
{
    public interface IBreweryService
    {
        /// <summary>
        /// Retrieves a page of breweries from Open Brewery DB, using the query cache when available.
        /// </summary>
        Task<PagedResponse<BreweryResponse>> GetAsync(BreweryQuery query, CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken ct);
    }
}