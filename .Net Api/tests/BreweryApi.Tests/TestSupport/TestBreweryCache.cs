using System.Collections.Concurrent;
using BreweryApi.Domain.Interfaces;

namespace BreweryApi.Tests.TestSupport;

public sealed class TestBreweryCache : IBreweryCache
{
    private readonly ConcurrentDictionary<string, object>
        values =
            new(StringComparer.Ordinal);

    public bool TryGet<T>(
        string key,
        out T? value)
    {
        if (values.TryGetValue(
                key,
                out var cachedValue)
            &&
            cachedValue is T typedValue)
        {
            value = typedValue;

            return true;
        }

        value = default;

        return false;
    }

    public void Set<T>(
        string key,
        T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            key);

        ArgumentNullException.ThrowIfNull(
            value);

        values[key] = value;
    }

    public void Seed<T>(
        string key,
        T value)
    {
        Set(
            key,
            value);
    }

    public bool Contains(
        string key)
    {
        return values.ContainsKey(key);
    }

    public T? Get<T>(
        string key)
    {
        return values.TryGetValue(
                   key,
                   out var value)
               &&
               value is T typedValue
            ? typedValue
            : default;
    }
}
