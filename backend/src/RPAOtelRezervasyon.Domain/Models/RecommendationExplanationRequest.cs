namespace RPAOtelRezervasyon.Domain.Models;

public sealed record RecommendationExplanationHotel(
    int Rank,
    string Name,
    decimal PriceAmount,
    string Currency,
    int DistanceMeters,
    int DurationSeconds,
    DistanceKind DistanceKind,
    double Latitude,
    double Longitude,
    bool WalkingRouteRequested,
    int? WalkingDistanceMeters,
    int? WalkingDurationSeconds,
    int? StayNights = null,
    int? CommuteDays = null,
    decimal? LodgingSubtotal = null,
    decimal? LocalTransportSubtotal = null,
    decimal? AirportRoundTripCost = null,
    decimal? TotalTripCost = null,
    string? FormattedTotalTripCost = null,
    int? EventCenterDistanceMeters = null,
    string? FormattedEventCenterDistance = null);

public sealed record RecommendationExplanationPolicyOption(
    RecommendationStrategy Strategy,
    string SelectedHotelName,
    IReadOnlyList<string> AllowedReasonCodes);

/// <summary>Only recommendation evidence; personnel and provider credentials are deliberately absent.</summary>
public sealed record RecommendationExplanationRequest(
    string EventCenterName,
    IReadOnlyList<RecommendationExplanationHotel> RankedHotels,
    IReadOnlyList<string> UnresolvedHotels,
    IReadOnlyList<RecommendationExplanationPolicyOption> Options);

public sealed record RecommendationExplanationPolicyOutcome(
    RecommendationStrategy Strategy,
    IReadOnlyList<string> ReasonCodes);

public sealed record RecommendationExplanationOutcome
{
    public bool IsSuccess { get; }

    public IReadOnlyList<RecommendationExplanationPolicyOutcome> Options { get; }

    public string? Summary { get; }

    private RecommendationExplanationOutcome(
        bool isSuccess,
        IReadOnlyList<RecommendationExplanationPolicyOutcome> options,
        string? summary)
    {
        IsSuccess = isSuccess;
        Options = options;
        Summary = summary;
    }

    public static RecommendationExplanationOutcome Success(
        IReadOnlyList<RecommendationExplanationPolicyOutcome> options,
        string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(true, options, summary);
    }

    public static RecommendationExplanationOutcome Failure() => new(false, [], null);
}
