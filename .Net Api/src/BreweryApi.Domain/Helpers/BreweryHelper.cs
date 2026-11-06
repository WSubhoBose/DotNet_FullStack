using BreweryApi.Domain.Exceptions;
using BreweryApi.Domain.Models;

namespace BreweryApi.Domain.Helpers
{
    public static class BreweryHelper
    {
        /// <summary>
        /// Validates supported sorting, pagination and coordinates.
        /// </summary>
        public static void Validate(BreweryQuery query)
        {
            if (query.Page < 1)
            {
                throw new RequestValidationException("Page must be greater than or equal to 1.");
            }

            if (query.PageSize is < 1 or > 200)
            {
                throw new RequestValidationException("PageSize must be between 1 and 200.");
            }

            if (!string.IsNullOrWhiteSpace(query.SortBy))
            {
                var allowedSortFields = new[] { "name", "city", "distance" };
                if (!allowedSortFields.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
                {
                    throw new RequestValidationException("SortBy must be name, city, or distance.");
                }
            }

            if (!query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
                !query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
            {
                throw new RequestValidationException("SortDirection must be asc or desc.");
            }

            if (query.SortBy?.Equals("distance", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (query.Latitude is null || query.Longitude is null)
                {
                    throw new RequestValidationException("Latitude and longitude are required when sorting by distance.");
                }
            }

            if (query.Latitude is < -90 or > 90)
            {
                throw new RequestValidationException("Latitude must be between -90 and 90.");
            }

            if (query.Longitude is < -180 or > 180)
            {
                throw new RequestValidationException("Longitude must be between -180 and 180.");
            }
        }

        /// <summary>
        /// Validates brewery autocomplete input.
        /// </summary>
        public static void ValidateAutocomplete(string term, int limit)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new RequestValidationException("Autocomplete term is required.");
            }

            if (term.Trim().Length < 2)
            {
                throw new RequestValidationException("Autocomplete term must contain at least two characters.");
            }

            if (limit is < 1 or > 20)
            {
                throw new RequestValidationException("Autocomplete limit must be between 1 and 20.");
            }
        }

        /// <summary>
        /// Determines whether the current request requires distance-based sorting.
        /// </summary>
        public static bool IsDistanceSort(BreweryQuery query)
        {
            return query.SortBy?.Equals("distance", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}

