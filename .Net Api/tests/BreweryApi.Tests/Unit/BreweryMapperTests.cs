using BreweryApi.Application.Mapping;
using BreweryApi.Domain.Entities;
using Moq;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class BreweryMapperTests
{
    private readonly BreweryMapper sut =
        new();

    [Fact]
    public void Map_MapsCompleteSourceModel()
    {
        var source =
            new SourceBrewery
            {
                Id = "10-barrel",
                Name = "10 Barrel Brewing",
                BreweryType = "large",
                Address1 = "1501 E Street",
                Address2 = "Suite 100",
                Address3 = "Building A",
                City = "San Diego",
                StateProvince = "California",
                PostalCode = "92101",
                Country = "United States",
                Longitude = -117.1611m,
                Latitude = 32.7157m,
                Phone = "6195551234",
                WebsiteUrl =
                    "https://example.test",
                State = "California",
                Street = "1501 E Street"
            };

        var result =
            sut.Map(source);

        Assert.Equal(
            source.Id,
            result.Id);

        Assert.Equal(
            source.Name,
            result.Name);

        Assert.Equal(
            source.BreweryType,
            result.BreweryType);

        Assert.Equal(
            source.Address1,
            result.Address1);

        Assert.Equal(
            source.Address2,
            result.Address2);

        Assert.Equal(
            source.Address3,
            result.Address3);

        Assert.Equal(
            source.City,
            result.City);

        Assert.Equal(
            source.StateProvince,
            result.StateProvince);

        Assert.Equal(
            source.PostalCode,
            result.PostalCode);

        Assert.Equal(
            source.Country,
            result.Country);

        Assert.Equal(
            source.Longitude,
            result.Longitude);

        Assert.Equal(
            source.Latitude,
            result.Latitude);

        Assert.Equal(
            source.Phone,
            result.Phone);

        Assert.Equal(
            source.WebsiteUrl,
            result.WebsiteUrl);

        Assert.Equal(
            source.State,
            result.State);

        Assert.Equal(
            source.Street,
            result.Street);
    }

    [Fact]
    public void Map_NullableFields_AreHandled()
    {
        var source =
            new SourceBrewery
            {
                Id = "1",
                Name = "Test Brewery",
                City = "",
                Phone = null,
                WebsiteUrl = null,
                Latitude = null,
                Longitude = null
            };

        var result =
            sut.Map(source);

        Assert.Equal(
            "Test Brewery",
            result.Name);

        Assert.Equal(string.Empty,
            result.City);

        Assert.Null(
            result.Phone);

        Assert.Null(
            result.WebsiteUrl);

        Assert.Null(
            result.Latitude);

        Assert.Null(
            result.Longitude);
    }

    [Fact]
    public void Map_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<
            ArgumentNullException>(
            () => sut.Map(null!));
    }
}
