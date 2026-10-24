namespace RPAOtelRezervasyon.Api.Contracts;

/// <summary>Başarılı öneri yanıtı (AC-1, AC-2).</summary>
public sealed record RecommendationResponseDto(
    string Status,
    RankedHotelDto SelectedHotel,
    RationaleDto Rationale,
    IReadOnlyList<RankedHotelDto> RankedHotels,
    IReadOnlyList<string> UnresolvedHotels,
    NearestAirportDto? NearestAirport);

public sealed record NearestAirportDto(
    string Name,
    double Latitude,
    double Longitude,
    int StraightLineDistanceMeters,
    int RouteDistanceMeters,
    int DurationSeconds,
    string DistanceKind,
    PriceDto? AirportHotelRoundTripTransportationCost,
    bool UsesDefaultTaxiFare);

/// <summary>Sıralamadaki tek otel, gönderilen gecelik fiyatı ve yol metrikleri (AC-2, AC-3).</summary>
public sealed record RankedHotelDto(
    string Name,
    PriceDto Price,
    double Latitude,
    double Longitude,
    int DistanceMeters,
    int DurationSeconds,
    string DistanceKind,
    PriceDto? RoundTripTransportationCost,
    PriceDto? TotalNightlyCost,
    bool UsesDefaultTaxiFare,
    int? TaxiDistanceMeters,
    string? TaxiDistanceKind,
    decimal? TaxiOpeningFare,
    decimal? TaxiFarePerKm,
    TripCostDto? TripCost = null);

public sealed record TripCostDto(
    int StayNights,
    int CommuteDays,
    PriceDto LodgingSubtotal,
    PriceDto? LocalTransportSubtotal,
    PriceDto? AirportRoundTripCost,
    PriceDto? TotalCost);

/// <summary>İstekte gönderilen bir gecelik fiyat; doğrulanmış biçimiyle aynen geri döner (AC-2, AC-3).</summary>
public sealed record PriceDto(
    decimal Amount,
    string Currency);

/// <summary>Yapılandırılmış seçim gerekçesi (AC-2, G-2).</summary>
public sealed record RationaleDto(
    string HotelName,
    int DurationSeconds,
    int DistanceMeters,
    string DistanceKind,
    int EvaluatedHotelCount);
