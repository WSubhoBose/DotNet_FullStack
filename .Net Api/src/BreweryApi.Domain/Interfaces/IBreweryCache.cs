using BreweryApi.Domain.Models;

namespace BreweryApi.Domain.Interfaces
{
    public interface IBreweryCache
    {
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value);
    }
}
