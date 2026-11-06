using BreweryApi.Domain.Interfaces;

namespace BreweryApi.Domain.Helpers;

public sealed class DistanceCalculator : IDistanceCalculator
{
    private const double EarthRadiusKilometres = 6371.0088d;

    public double CalculateKilometres(
        decimal inputLatitude,
        decimal inputLongitude,
        decimal breweryLatitude,
        decimal breweryLongitude)
    {
        var inputLatRadians =
            ToRadians(
                (double)inputLatitude);

        var inputLonRadians =
            ToRadians(
                (double)inputLongitude);

        var breweryLatRadians =
            ToRadians(
                (double)breweryLatitude);

        var breweryLonRadians =
            ToRadians(
                (double)breweryLongitude);

        var latitudeDifference =
            breweryLatRadians -
            inputLatRadians;

        var longitudeDifference =
            breweryLonRadians -
            inputLonRadians;

        var a =
            Math.Pow(
                Math.Sin(
                    latitudeDifference / 2d),
                2d)
            +
            Math.Cos(inputLatRadians)
            *
            Math.Cos(breweryLatRadians)
            *
            Math.Pow(
                Math.Sin(
                    longitudeDifference / 2d),
                2d);

        var c =
            2d *
            Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1d - a));

        return EarthRadiusKilometres *
               c;
    }

    private static double ToRadians(
        double degrees)
    {
        return degrees *
               Math.PI /
               180d;
    }
}