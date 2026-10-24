using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.VenueGeocoding;

/// <summary>Etkinlik alanı adını konuma çevirir.</summary>
public sealed class VenueGeocodingService(IGeocodingProvider geocodingProvider)
{
    public Task<GeocodingOutcome> ResolveAsync(string eventCenterName, CancellationToken cancellationToken) =>
        geocodingProvider.GeocodeAsync(eventCenterName, cancellationToken);

    public Task<GeocodingOutcome> ResolveAsync(
        string eventCenterName,
        GeocodingContext context,
        CancellationToken cancellationToken) =>
        geocodingProvider.GeocodeAsync(eventCenterName, context, cancellationToken);
}
