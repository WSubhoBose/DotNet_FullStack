using System.Globalization;
using BreweryApi.Domain;
using BreweryApi.Domain.Helpers;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BreweryApi.Infrastructure;

public sealed class SqliteBreweryCatalogService(
    IDbContextFactory<BreweryDbContext> contextFactory,
    IOpenBreweryClient openBreweryClient,
    IDistanceCalculator distanceCalculator,
    IConfiguration configuration,
    ILogger<SqliteBreweryCatalogService> logger) : IBreweryCatalogService
{
    private const int SourcePageSize = 200;
    private static readonly SemaphoreSlim CatalogSyncLock = new(1, 1);
    private readonly double refreshIntervalHours = ReadRefreshIntervalHours(configuration);

    public async Task<PagedResponse<BreweryResponse>> GetAsync(
        BreweryQuery query,
        CancellationToken cancellationToken)
    {
        BreweryHelper.Validate(query);
        await EnsureCatalogAsync(cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var breweries = ApplyFilters(db.Breweries.AsNoTracking(), query);
        var totalCount = await breweries.CountAsync(cancellationToken);
        List<BreweryRecord> records;

        if (BreweryHelper.IsDistanceSort(query))
        {
            records = await breweries.ToListAsync(cancellationToken);
            var sorted = SortByDistance(records.Select(ToResponse).ToList(), query);
            var page = sorted
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return new PagedResponse<BreweryResponse>(page, query.Page, query.PageSize, totalCount);
        }

        var ordered = ApplySort(breweries, query);
        records = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BreweryResponse>(
            records.Select(ToResponse).ToList(),
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        BreweryHelper.ValidateAutocomplete(term, limit);
        await EnsureCatalogAsync(cancellationToken);

        var pattern = CreateLikePattern(term);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var breweries = await db.Breweries
            .AsNoTracking()
            .Where(brewery =>
                EF.Functions.Like(brewery.Name, pattern, "\\") ||
                EF.Functions.Like(brewery.BreweryType, pattern, "\\") ||
                EF.Functions.Like(brewery.City, pattern, "\\") ||
                EF.Functions.Like(brewery.StateProvince, pattern, "\\") ||
                EF.Functions.Like(brewery.Country, pattern, "\\") ||
                EF.Functions.Like(brewery.PostalCode, pattern, "\\"))
            .Select(brewery => new
            {
                brewery.Name,
                brewery.BreweryType,
                brewery.City,
                brewery.StateProvince,
                brewery.Country,
                brewery.PostalCode
            })
            .ToListAsync(cancellationToken);

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var brewery in breweries)
        {
            AddValue(brewery.Name);
            AddValue(brewery.BreweryType);
            AddValue(brewery.City);
            AddValue(brewery.StateProvince);
            AddValue(brewery.Country);
            AddValue(brewery.PostalCode);
        }

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        void AddValue(string? value)
        {
            var trimmed = value?.Trim();
            if (!string.IsNullOrEmpty(trimmed) &&
                trimmed.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                values.Add(trimmed);
            }
        }
    }

    private async Task EnsureCatalogAsync(CancellationToken cancellationToken)
    {
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            if (!await IsRefreshRequiredAsync(db, cancellationToken))
            {
                return;
            }
        }

        await CatalogSyncLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await IsRefreshRequiredAsync(db, cancellationToken))
            {
                return;
            }

            var catalog = new List<BreweryResponse>();
            var isComplete = false;
            for (var page = 1; page <= 1000; page++)
            {
                var response = await openBreweryClient.QueryAsync(
                    new BreweryQuery(Page: page, PageSize: SourcePageSize),
                    cancellationToken);
                catalog.AddRange(response.Breweries);
                if (response.Breweries.Count < SourcePageSize)
                {
                    isComplete = true;
                    break;
                }
            }

            if (!isComplete)
            {
                throw new InvalidOperationException("The upstream brewery catalog exceeded the supported synchronization limit.");
            }

            if (catalog.Count == 0)
            {
                throw new InvalidOperationException("The upstream brewery catalog returned no records.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Breweries.RemoveRange(await db.Breweries.ToListAsync(cancellationToken));
            db.Breweries.AddRange(catalog.Select(ToRecord));

            var metadata = await db.CatalogMetadata
                .SingleOrDefaultAsync(value => value.Key == "LastSyncedAtUtc", cancellationToken);
            if (metadata is null)
            {
                db.CatalogMetadata.Add(new CatalogMetadata
                {
                    Key = "LastSyncedAtUtc",
                    Value = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                });
            }
            else
            {
                metadata.Value = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            CatalogSyncLock.Release();
        }
    }

    private async Task<bool> IsRefreshRequiredAsync(
        BreweryDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Breweries.AnyAsync(cancellationToken))
        {
            return true;
        }

        var lastSynced = await db.CatalogMetadata
            .AsNoTracking()
            .Where(value => value.Key == "LastSyncedAtUtc")
            .Select(value => value.Value)
            .SingleOrDefaultAsync(cancellationToken);
        if (lastSynced is null)
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(lastSynced, CultureInfo.InvariantCulture, DateTimeStyles.None, out var syncedAt))
        {
            throw new InvalidOperationException("The saved brewery catalog synchronization timestamp is invalid.");
        }

        return DateTimeOffset.UtcNow - syncedAt >= TimeSpan.FromHours(refreshIntervalHours);
    }

    private static IQueryable<BreweryRecord> ApplyFilters(
        IQueryable<BreweryRecord> breweries,
        BreweryQuery query)
    {
        var name = !string.IsNullOrWhiteSpace(query.Name) ? query.Name : query.Search;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = CreateLikePattern(name);
            breweries = breweries.Where(brewery => EF.Functions.Like(brewery.Name, pattern, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var pattern = CreateLikePattern(query.City);
            breweries = breweries.Where(brewery => EF.Functions.Like(brewery.City, pattern, "\\"));
        }

        return breweries;
    }

    private static IOrderedQueryable<BreweryRecord> ApplySort(
        IQueryable<BreweryRecord> breweries,
        BreweryQuery query)
    {
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        return query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending
                ? breweries.OrderByDescending(brewery => EF.Functions.Collate(brewery.Name, "NOCASE"))
                    .ThenBy(brewery => EF.Functions.Collate(brewery.Id, "NOCASE"))
                : breweries.OrderBy(brewery => EF.Functions.Collate(brewery.Name, "NOCASE"))
                    .ThenBy(brewery => EF.Functions.Collate(brewery.Id, "NOCASE")),
            "city" => descending
                ? breweries.OrderByDescending(brewery => EF.Functions.Collate(brewery.City, "NOCASE"))
                    .ThenBy(brewery => EF.Functions.Collate(brewery.Name, "NOCASE"))
                : breweries.OrderBy(brewery => EF.Functions.Collate(brewery.City, "NOCASE"))
                    .ThenBy(brewery => EF.Functions.Collate(brewery.Name, "NOCASE")),
            _ => breweries.OrderBy(brewery => EF.Functions.Collate(brewery.Id, "NOCASE"))
        };
    }

    private List<BreweryResponse> SortByDistance(
        IReadOnlyCollection<BreweryResponse> breweries,
        BreweryQuery query)
    {
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        return breweries
            .Select(brewery =>
            {
                var distance = brewery.Latitude.HasValue && brewery.Longitude.HasValue
                    ? distanceCalculator.CalculateKilometres(
                        query.Latitude!.Value,
                        query.Longitude!.Value,
                        brewery.Latitude.Value,
                        brewery.Longitude.Value)
                    : (double?)null;
                return (Brewery: brewery, Distance: distance);
            })
            .OrderBy(item => item.Distance.HasValue ? 0 : 1)
            .ThenBy(item => item.Distance ?? (descending ? double.MinValue : double.MaxValue),
                descending ? Comparer<double>.Create((left, right) => right.CompareTo(left)) : Comparer<double>.Default)
            .ThenBy(item => item.Brewery.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Brewery with
            {
                Distance = item.Distance?.ToString("0.00", CultureInfo.InvariantCulture) +
                    (item.Distance.HasValue ? " km" : null)
            })
            .ToList();
    }

    private BreweryRecord ToRecord(BreweryResponse brewery) => new()
    {
        Id = RequiredText(brewery.Id, nameof(brewery.Id), brewery.Id),
        Name = RequiredText(brewery.Name, nameof(brewery.Name), brewery.Id),
        BreweryType = RequiredText(brewery.BreweryType, nameof(brewery.BreweryType), brewery.Id),
        Address1 = brewery.Address1,
        Address2 = brewery.Address2,
        Address3 = brewery.Address3,
        City = RequiredText(brewery.City, nameof(brewery.City), brewery.Id),
        StateProvince = RequiredText(brewery.StateProvince, nameof(brewery.StateProvince), brewery.Id),
        PostalCode = RequiredText(brewery.PostalCode, nameof(brewery.PostalCode), brewery.Id),
        Country = RequiredText(brewery.Country, nameof(brewery.Country), brewery.Id),
        Longitude = brewery.Longitude,
        Latitude = brewery.Latitude,
        Phone = brewery.Phone,
        WebsiteUrl = brewery.WebsiteUrl,
        State = brewery.State,
        Street = brewery.Street
    };

    private static BreweryResponse ToResponse(BreweryRecord brewery) => new(
        brewery.Id,
        brewery.Name,
        brewery.BreweryType,
        brewery.Address1,
        brewery.Address2,
        brewery.Address3,
        brewery.City,
        brewery.StateProvince,
        brewery.PostalCode,
        brewery.Country,
        brewery.Longitude,
        brewery.Latitude,
        brewery.Phone,
        brewery.WebsiteUrl,
        brewery.State,
        brewery.Street);

    private string RequiredText(string? value, string fieldName, string breweryId)
    {
        if (value is not null)
        {
            return value;
        }

        logger.LogWarning(
            "The upstream brewery record {BreweryId} has no {FieldName}; storing an empty value.",
            breweryId,
            fieldName);
        return string.Empty;
    }

    private static string CreateLikePattern(string value)
    {
        var escaped = value.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private static double ReadRefreshIntervalHours(IConfiguration configuration)
    {
        var hours = configuration.GetValue<double?>("Data:CatalogRefreshHours") ?? 24;
        if (!double.IsFinite(hours) || hours <= 0)
        {
            throw new InvalidOperationException("Data:CatalogRefreshHours must be a positive finite number.");
        }

        return hours;
    }
}
