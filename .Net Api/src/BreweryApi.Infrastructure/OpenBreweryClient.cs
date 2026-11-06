using BreweryApi.Domain;
using BreweryApi.Domain.Helpers;
using BreweryApi.Domain.Interfaces;
using BreweryApi.Domain.Models;
using BreweryApi.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Json;

namespace BreweryApi.Infrastructure
{
    public sealed class OpenBreweryClient(HttpClient http, IBreweryMapper breweryMapper, ILogger<OpenBreweryClient> logger) : IOpenBreweryClient
    {
        /// <summary>
        /// Retrieves breweries from Open Brewery DB using filtering,
        /// sorting and pagination parameters.
        /// </summary>
        public async Task<PagedResponse<BreweryResponse>> QueryAsync(BreweryQuery query, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "breweries")
            {
                Content = JsonContent.Create(BuildRequestBody(query))
            };

            using var response = await http.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            var sourceBreweries = await response.Content.ReadFromJsonAsync<List<SourceBrewery>>(cancellationToken: cancellationToken) ?? [];

            var breweries = sourceBreweries.Select(breweryMapper.Map).ToList();

            if (BreweryHelper.IsDistanceSort(query))
            {
                breweries = OrderByDistance(breweries, query);
            }

            return new PagedResponse<BreweryResponse>(
                breweries,
                query.Page,
                query.PageSize,
                null);
        }

        /// <summary>
        /// Retrieves brewery-name suggestions directly from Open Brewery DB.
        /// </summary>
        public async Task<IReadOnlyList<string>> AutocompleteAsync(string term, int limit, CancellationToken cancellationToken)
        {
            BreweryHelper.ValidateAutocomplete(term, limit);
            var normalisedTerm = term.Trim();

            /*
             * Retrieve more source rows than the final limit because
             * several breweries can produce duplicate suggestion values.
             */
            var sourceLimit = Math.Min(limit * 3, 60);

            var requests =
                AutocompleteFieldFilters
                    .Select(
                        filter =>
                            GetAutocompleteCandidatesAsync(
                                filter,
                                normalisedTerm,
                                sourceLimit,
                                cancellationToken))
                    .ToList();

            var breweryTypeRequest =
                CreateBreweryTypeRequest(
                    normalisedTerm,
                    sourceLimit,
                    cancellationToken);

            if (breweryTypeRequest is not null)
            {
                requests.Add(
                    breweryTypeRequest);
            }

            var resultSets =
                await Task.WhenAll(
                    requests);

            return resultSets
                .SelectMany(
                    values => values)
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    value => value.Trim())
                .Where(
                    value =>
                        value.Contains(
                            normalisedTerm,
                            StringComparison.OrdinalIgnoreCase))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    value => value,
                    StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Builds Open Brewery DB request body parameters from the application query.
        /// </summary>
        private static Dictionary<string, object?> BuildRequestBody(BreweryQuery query)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["page"] = query.Page,
                ["per_page"] = query.PageSize
            };

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                parameters["by_name"] = NormaliseValue(query.Search);
            }

            if (!string.IsNullOrWhiteSpace(query.Name))
            {
                parameters["by_name"] = NormaliseValue(query.Name);
            }

            if (!string.IsNullOrWhiteSpace(query.City))
            {
                parameters["by_city"] = NormaliseValue(query.City);
            }

            if (query.SortBy?.Equals("distance", StringComparison.OrdinalIgnoreCase) == true)
            {
                parameters["by_dist"] =
                    string.Concat(query.Latitude!.Value.ToString(CultureInfo.InvariantCulture), ",",
                    query.Longitude!.Value.ToString(CultureInfo.InvariantCulture));
            }
            else if (!string.IsNullOrWhiteSpace(query.SortBy))
            {
                var sortBy = query.SortBy.Trim().ToLowerInvariant();
                var sortDirection = query.SortDirection?.Trim().ToLowerInvariant() ?? "asc";
                var sortValue = $"type,{sortBy}:{sortDirection}";
                parameters["sort"] = sortValue;
            }

            return parameters;
        }

        /// <summary>
        /// Normalises a filter value for Open Brewery DB.
        /// </summary>
        private static string NormaliseValue(string value)
        {
            return value.Trim().Replace(" ", "_").ToLowerInvariant();
        }

        private Task<IReadOnlyList<string>>? CreateBreweryTypeRequest(
            string term,
            int sourceLimit,
            CancellationToken cancellationToken)
        {
            var matchedType =
                SupportedBreweryTypes
                    .FirstOrDefault(
                        type =>
                            type.StartsWith(
                                term,
                                StringComparison.OrdinalIgnoreCase));

            if (matchedType is null)
            {
                return null;
            }

            var filter =
                new AutocompleteFilter(
                    "by_type",
                    brewery =>
                        brewery.BreweryType);

            return GetAutocompleteCandidatesAsync(
                filter,
                matchedType,
                sourceLimit,
                cancellationToken);
        }

        private static readonly string[] SupportedBreweryTypes =
        [
            "micro",
            "nano",
            "regional",
            "brewpub",
            "large",
            "planning",
            "bar",
            "contract",
            "proprietor",
            "closed"
        ];

        public static readonly IReadOnlyList<AutocompleteFilter> AutocompleteFieldFilters =
        [
            new(
                "by_name",
                brewery => brewery.Name),

            new(
                "by_city",
                brewery => brewery.City),

            new(
                "by_state",
                brewery =>
                    brewery.StateProvince
                    ?? brewery.State),

            new(
                "by_country",
                brewery => brewery.Country),

            new(
                "by_postal",
                brewery => brewery.PostalCode)
        ];

        private List<BreweryResponse> OrderByDistance(IEnumerable<BreweryResponse> breweries, BreweryQuery query)
        {
            var inputLatitude = query.Latitude!.Value;
            var inputLongitude = query.Longitude!.Value;
            var isDescending = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;

            return breweries.Select(
                    brewery =>
                    {
                        var distance = GetDistance(brewery, inputLatitude, inputLongitude);
                        return new BreweryWithDistance(brewery, distance);
                    })
                .OrderBy(item => item.DistanceKilometres.HasValue ? 0 : 1)
                .ThenBy(
                    item => item.DistanceKilometres ?? (isDescending ? double.MinValue : double.MaxValue),
                    isDescending ? Comparer<double>.Create((x, y) => y.CompareTo(x)) : Comparer<double>.Default)
                .ThenBy(item => item.Brewery.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                        item.Brewery with
                        {
                            Distance = FormatDistance(item.DistanceKilometres)
                        })
                .ToList();
        }

        private static string? FormatDistance(double? distanceKilometres)
        {
            if (!distanceKilometres.HasValue)
            {
                return null;
            }

            return string.Concat(distanceKilometres.Value.ToString("0.00", CultureInfo.InvariantCulture), " km");
        }

        private double? GetDistance(BreweryResponse brewery, decimal inputLatitude, decimal inputLongitude)
        {
            if (!brewery.Latitude.HasValue ||
                !brewery.Longitude.HasValue)
            {
                return null;
            }

            return new DistanceCalculator()
                .CalculateKilometres(
                    inputLatitude,
                    inputLongitude,
                    brewery.Latitude.Value,
                    brewery.Longitude.Value);
        }

        private async Task<IReadOnlyList<string>> GetAutocompleteCandidatesAsync(
                AutocompleteFilter filter,
                string term,
                int sourceLimit,
                CancellationToken cancellationToken)
        {
            var requestBody = new Dictionary<string, object?>
            {
                [filter.QueryParameter] = term,
                ["page"] = 1,
                ["per_page"] = sourceLimit
            };

            logger.LogDebug(
                "Executing autocomplete source query. " +
                "Filter: {FilterName}",
                filter.QueryParameter);

            using var request = new HttpRequestMessage(HttpMethod.Get, "breweries")
            {
                Content = JsonContent.Create(requestBody)
            };
            using var response =
                await http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            var breweries =
                await response.Content
                    .ReadFromJsonAsync<
                        List<SourceBrewery>>(
                        cancellationToken:
                            cancellationToken)
                ?? [];

            return breweries
                .Select(filter.Selector)
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    value => value!.Trim())
                .ToList();
        }
    }
}