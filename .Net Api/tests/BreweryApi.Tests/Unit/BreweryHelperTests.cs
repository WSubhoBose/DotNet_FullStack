using BreweryApi.Domain.Exceptions;
using BreweryApi.Domain.Helpers;
using BreweryApi.Domain.Models;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class BreweryHelperTests
{
    [Fact]
    public void Validate_ValidQuery_DoesNotThrow()
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "name",
                SortDirection = "asc"
            };

        BreweryHelper.Validate(
            query);
    }

    [Fact]
    public void Validate_PageZero_Throws()
    {
        var query =
            new BreweryQuery
            {
                Page = 0,
                PageSize = 50
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Fact]
    public void Validate_NegativePage_Throws()
    {
        var query =
            new BreweryQuery
            {
                Page = -1,
                PageSize = 50
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void Validate_InvalidPageSize_Throws(
        int pageSize)
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = pageSize
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Theory]
    [InlineData("anything")]
    [InlineData("state")]
    [InlineData("type")]
    public void Validate_InvalidSortBy_Throws(
        string sortBy)
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = sortBy
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("random")]
    public void Validate_InvalidSortDirection_Throws(
        string direction)
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "name",
                SortDirection = direction
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Fact]
    public void Validate_DistanceWithoutCoordinates_ThrowsExpectedMessage()
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "distance"
            };

        var exception =
            Assert.Throws<
                RequestValidationException>(
                () =>
                    BreweryHelper.Validate(
                        query));

        Assert.Equal(
            "Latitude and longitude are required " +
            "when sorting by distance.",
            exception.Message);
    }

    [Fact]
    public void Validate_DistanceWithoutLatitude_Throws()
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "distance",
                Longitude = -117.1611m
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Fact]
    public void Validate_DistanceWithoutLongitude_Throws()
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "distance",
                Latitude = 32.7157m
            };

        Assert.Throws<
            RequestValidationException>(
            () => BreweryHelper.Validate(query));
    }

    [Fact]
    public void Validate_DistanceWithCoordinates_DoesNotThrow()
    {
        var query =
            new BreweryQuery
            {
                Page = 1,
                PageSize = 50,
                SortBy = "distance",
                Latitude = 32.7157m,
                Longitude = -117.1611m
            };

        BreweryHelper.Validate(
            query);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validate_LatitudeOutsideRange_Throws(decimal latitude)
    {
        var query = new BreweryQuery
        {
            Page = 1,
            PageSize = 50,
            Latitude = latitude
        };

        Assert.Throws<RequestValidationException>(() => BreweryHelper.Validate(query));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Validate_LongitudeOutsideRange_Throws(decimal longitude)
    {
        var query = new BreweryQuery
        {
            Page = 1,
            PageSize = 50,
            Longitude = longitude
        };

        Assert.Throws<RequestValidationException>(() => BreweryHelper.Validate(query));
    }

    [Fact]
    public void IsDistanceSort_Distance_ReturnsTrue()
    {
        var query =
            new BreweryQuery
            {
                SortBy = "distance"
            };

        Assert.True(
            BreweryHelper.IsDistanceSort(
                query));
    }

    [Fact]
    public void IsDistanceSort_Name_ReturnsFalse()
    {
        var query =
            new BreweryQuery
            {
                SortBy = "name"
            };

        Assert.False(
            BreweryHelper.IsDistanceSort(
                query));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateAutocomplete_InvalidTerm_Throws(
        string term)
    {
        Assert.Throws<
            RequestValidationException>(
            () =>
                BreweryHelper.ValidateAutocomplete(
                    term,
                    10));
    }

    [Fact]
    public void ValidateAutocomplete_SingleCharacterTerm_Throws()
    {
        Assert.Throws<RequestValidationException>(
            () => BreweryHelper.ValidateAutocomplete("a", 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateAutocomplete_InvalidLimit_Throws(
        int limit)
    {
        Assert.Throws<
            RequestValidationException>(
            () =>
                BreweryHelper.ValidateAutocomplete(
                    "Alpha",
                    limit));
    }

    [Fact]
    public void ValidateAutocomplete_LimitAboveMaximum_Throws()
    {
        Assert.Throws<RequestValidationException>(
            () => BreweryHelper.ValidateAutocomplete("Alpha", 21));
    }
}