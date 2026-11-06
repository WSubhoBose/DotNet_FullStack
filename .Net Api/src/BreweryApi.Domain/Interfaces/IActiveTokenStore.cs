using BreweryApi.Domain.Entities;

namespace BreweryApi.Domain.Interfaces;

public interface IActiveTokenStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(string token, CancellationToken cancellationToken);

    Task<ActiveTokenUser?> GetActiveUserAsync(string token, CancellationToken cancellationToken);
}
