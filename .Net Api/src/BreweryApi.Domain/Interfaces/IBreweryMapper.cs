using BreweryApi.Domain.Models;
using BreweryApi.Domain.Entities;

namespace BreweryApi.Domain.Interfaces;

public interface IBreweryMapper
{
    BreweryResponse Map(SourceBrewery source);
}
