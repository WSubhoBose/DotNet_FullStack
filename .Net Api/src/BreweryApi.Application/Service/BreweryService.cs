using System.Collections.Concurrent;
using BreweryApi.Domain;
using BreweryApi.Domain.Helpers;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BreweryApi.Application.Service
{
    public sealed class BreweryService(IBreweryCache breweryCache, IOpenBreweryClient openBreweryClient, ILogger<BreweryService> logger) : IBreweryService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> inFlight = new(StringComparer.Ordinal);

        public async Task<PagedResponse<BreweryResponse>> GetAsync(BreweryQuery query, CancellationToken cancellationToken)
        {
            BreweryHelper.Validate(query);

            var cacheKey = BreweryCacheKey.Create(query);

            if (breweryCache.TryGet<PagedResponse<BreweryResponse>>(cacheKey, out var cached) && cached is not null)
            {
                logger.LogDebug(
                    "Returning brewery query from cache. " +
                    "CacheKey: {CacheKey}",
                    cacheKey);

                return cached;
            }

            logger.LogInformation(
                "No Record found in Brewery query cache. " +
                "CacheKey: {CacheKey}",
                cacheKey);

            return await GetOrCreateAsync(cacheKey, () => openBreweryClient.QueryAsync(query, cancellationToken), cancellationToken);
        }

        public async Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken cancellationToken)
        {
            BreweryHelper.ValidateAutocomplete(term, limit);
            var normalisedTerm = term.Trim();
            var cacheKey = BreweryCacheKey.CreateAutocomplete(normalisedTerm, limit);

            if (breweryCache.TryGet<IReadOnlyList<string>>(cacheKey, out var breweries) && breweries is not null)
            {
                logger.LogInformation("Brewery catalogue cache hit for autocomplete.");
                return breweries;
            }

            logger.LogDebug("Brewery catalogue cache miss for autocomplete.");

            return await GetOrCreateAsync(cacheKey, () => openBreweryClient.AutocompleteAsync(normalisedTerm, limit, cancellationToken), cancellationToken);
        }

        private async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> factory, CancellationToken cancellationToken)
            where T : class
        {
            if (breweryCache.TryGet<T>(cacheKey, out var cached) && cached is not null)
            {
                return cached;
            }

            var semaphore = inFlight.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(cancellationToken);

            try
            {
                if (breweryCache.TryGet<T>(cacheKey, out cached) && cached is not null)
                {
                    return cached;
                }

                var value = await factory();

                if (value is null)
                {
                    return null!;
                }

                breweryCache.Set(cacheKey, value);
                return value;
            }
            finally
            {
                semaphore.Release();
                inFlight.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, semaphore));
            }
        }
    }
}
