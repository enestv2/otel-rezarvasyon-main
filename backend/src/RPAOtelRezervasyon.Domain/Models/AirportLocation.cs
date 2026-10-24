namespace RPAOtelRezervasyon.Domain.Models;

public sealed record AirportCandidate(string Name, GeoPoint Location, int StraightLineDistanceMeters);

public sealed record NearestAirportInfo(
    string Name,
    GeoPoint Location,
    int StraightLineDistanceMeters,
    RouteMetrics Route,
    TaxiFareEstimate? AirportHotelRoundTripFare = null);
