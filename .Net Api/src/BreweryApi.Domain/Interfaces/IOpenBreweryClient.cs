using BreweryApi.Domain.Models;

namespace BreweryApi.Domain.Interfaces
{
    public interface IOpenBreweryClient
    {
        /// <summary>
        /// Retrieves a requested page directly from Open Brewery DB.
        /// </summary>
        Task<PagedResponse<BreweryResponse>> QueryAsync(BreweryQuery query, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves brewery name suggestions directly from Open Brewery DB.
        /// </summary>
        Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken cancellationToken);
    }
}
