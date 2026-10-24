using RPAOtelRezervasyon.Application.Modules.DistanceRouting;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.Airports;

public sealed class AirportSearchService(
    IAirportSearchProvider airportSearchProvider,
    DistanceRoutingService distanceRoutingService)
{
    public async Task<NearestAirportInfo?> FindNearestAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);

        try
        {
            var airports = await airportSearchProvider.FindNearbyAirportsAsync(location, cancellationToken).ConfigureAwait(false);
            foreach (var airport in airports)
            {
                var route = await distanceRoutingService.ResolveMetricsAsync(location, airport.Location, cancellationToken)
                    .ConfigureAwait(false);
                if (route is { Kind: DistanceKind.Road })
                {
                    return new NearestAirportInfo(airport.Name, airport.Location, airport.StraightLineDistanceMeters, route);
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Havalimanı servisi erişilemezse otel önerisi ve PDF üretimi devam eder.
            return null;
        }
    }
}
