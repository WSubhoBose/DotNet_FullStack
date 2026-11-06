using System;

namespace BreweryApi.Domain.Interfaces;

public interface IDistanceCalculator
{
    double CalculateKilometres(
        decimal inputLatitude,
        decimal inputLongitude,
        decimal breweryLatitude,
        decimal breweryLongitude);
}
