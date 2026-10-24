using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

public interface IAirportSearchProvider
{
    Task<AirportCandidate?> FindNearestAirportAsync(GeoPoint location, CancellationToken cancellationToken);

    async Task<IReadOnlyList<AirportCandidate>> FindNearbyAirportsAsync(
        GeoPoint location,
        CancellationToken cancellationToken)
    {
        var nearest = await FindNearestAirportAsync(location, cancellationToken).ConfigureAwait(false);
        return nearest is null ? [] : [nearest];
    }
}
