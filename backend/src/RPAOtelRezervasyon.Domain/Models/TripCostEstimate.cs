namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Görev tarihleri için konaklama ve tahmini ulaşımın toplam maliyeti.</summary>
public sealed record TripCostEstimate(
    int StayNights,
    int CommuteDays,
    NightlyPrice LodgingSubtotal,
    NightlyPrice? LocalTransportSubtotal,
    NightlyPrice? AirportRoundTripCost,
    NightlyPrice? TotalCost)
{
    public static TripCostEstimate Calculate(
        NightlyPrice nightlyPrice,
        TaxiFareEstimate? localRoundTripFare,
        TaxiFareEstimate? airportRoundTripFare,
        int stayNights,
        int commuteDays)
    {
        ArgumentNullException.ThrowIfNull(nightlyPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(stayNights);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(commuteDays);

        var lodging = new NightlyPrice(nightlyPrice.Amount * stayNights, nightlyPrice.Currency);
        var localTransport = localRoundTripFare is null
            ? null
            : new NightlyPrice(localRoundTripFare.RoundTripCost.Amount * commuteDays, nightlyPrice.Currency);
        var airport = airportRoundTripFare?.RoundTripCost;
        var total = localTransport is null || airport is null
            ? null
            : new NightlyPrice(lodging.Amount + localTransport.Amount + airport.Amount, nightlyPrice.Currency);

        return new TripCostEstimate(stayNights, commuteDays, lodging, localTransport, airport, total);
    }
}
