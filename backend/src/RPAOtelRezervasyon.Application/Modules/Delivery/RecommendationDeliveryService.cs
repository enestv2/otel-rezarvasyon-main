using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.Delivery;

/// <summary>
/// V1'de teslim, çağırana dönen sonuçtur (REST gövdesi). İleride kuyruk/olay eklenirse burası genişler.
/// </summary>
public sealed class RecommendationDeliveryService
{
    public RecommendationResult Deliver(
        string eventCenterName,
        GeoPoint venueLocation,
        Recommendation recommendation,
        IReadOnlyList<string> unresolvedHotels,
        bool preferCityCenter = false,
        NearestAirportInfo? nearestAirport = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventCenterName);
        ArgumentNullException.ThrowIfNull(venueLocation);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(unresolvedHotels);

        var rationale = Rationale.For(recommendation.Selected, recommendation.RankedHotels.Count);

        return RecommendationResult.Succeeded(eventCenterName, venueLocation, recommendation, rationale, unresolvedHotels,
            preferCityCenter, nearestAirport);
    }
}
