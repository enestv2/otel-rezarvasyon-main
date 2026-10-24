namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Şehir taksi tarifesi. LLM kaynağı verilemiyorsa bu fiyat kullanılmaz.</summary>
public sealed record TaxiFareQuote(
    string City,
    decimal OpeningFare,
    decimal FarePerKm,
    string Currency,
    DateOnly EffectiveDate,
    string SourceUrl);

public sealed record TaxiFareEstimate(
    NightlyPrice RoundTripCost,
    bool UsedDefaultFare,
    int DistanceMeters,
    DistanceKind DistanceKind,
    decimal OpeningFare,
    decimal FarePerKm);
