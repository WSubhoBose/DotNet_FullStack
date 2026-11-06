using BreweryApi.Domain.Models;

namespace BreweryApi.Domain.Interfaces;

public interface IBreweryCatalogService
{
    Task<PagedResponse<BreweryResponse>> GetAsync(BreweryQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken cancellationToken);
}
