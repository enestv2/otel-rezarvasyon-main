namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// Sıralamaya girebilecek otel: konumu ve yol metrikleri **çözümlenmiş** olmalıdır.
/// Konumu ya da metriği olmayan otel aday olamaz (AC-7).
/// </summary>
public sealed record HotelCandidate
{
    public ContractedHotel Hotel { get; }

    public GeoPoint Location { get; }

    public RouteMetrics Metrics { get; }

    public TaxiFareEstimate? TaxiFareEstimate { get; }

    public NightlyPrice? TotalNightlyCost { get; }

    public NearestAirportInfo? NearestAirport { get; }

    public TripCostEstimate? TripCostEstimate { get; }

    public HotelCandidate(
        ContractedHotel hotel,
        GeoPoint location,
        RouteMetrics metrics,
        TaxiFareEstimate? taxiFareEstimate = null,
        NearestAirportInfo? nearestAirport = null,
        TripCostEstimate? tripCostEstimate = null)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(metrics);

        Hotel = hotel;
        Location = location;
        Metrics = metrics;
        TaxiFareEstimate = taxiFareEstimate;
        NearestAirport = nearestAirport;
        TripCostEstimate = tripCostEstimate;
        TotalNightlyCost = taxiFareEstimate is null
            ? null
            : new NightlyPrice(hotel.Price.Amount + taxiFareEstimate.RoundTripCost.Amount, hotel.Price.Currency);
    }
}
