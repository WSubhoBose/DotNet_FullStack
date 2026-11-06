using BreweryApi.Domain.Models;
using BreweryApi.Domain.Entities;
using BreweryApi.Domain.Interfaces;

namespace BreweryApi.Application.Mapping;

public sealed class BreweryMapper : IBreweryMapper
{
    public BreweryResponse Map(SourceBrewery source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new BreweryResponse(
            Id: source.Id,
            Name: source.Name,
            BreweryType: source.BreweryType,
            Address1: source.Address1,
            Address2: source.Address2,
            Address3: source.Address3,
            City: source.City,
            StateProvince: source.StateProvince,
            PostalCode: source.PostalCode,
            Country: source.Country,
            Longitude: source.Longitude,
            Latitude: source.Latitude,
            Phone: source.Phone,
            WebsiteUrl: source.WebsiteUrl,
            State: source.State,
            Street: source.Street);
    }
}
