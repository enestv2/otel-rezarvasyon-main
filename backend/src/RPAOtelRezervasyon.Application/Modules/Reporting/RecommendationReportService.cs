using System.Globalization;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;

namespace RPAOtelRezervasyon.Application.Modules.Reporting;

/// <summary>
/// Başarılı öneriyi rapor modeline dönüştürür, uygun oteller için ek yürüyüş metriklerini alır ve harita/PDF üretimini yürütür.
/// Ek yürüyüş değerleri mevcut JSON öneri metriklerini değiştirmez; harita üretilemese de belge oluşturulur.
/// </summary>
public sealed class RecommendationReportService(
    IStaticMapProvider staticMapProvider,
    IRecommendationReportRenderer renderer,
    IWalkingRouteDistanceProvider walkingRouteDistanceProvider,
    RecommendationExplanationService explanationService,
    ReportingOptions options)
{
    public async Task<ReportDocument> CreateAsync(
        string eventCenterName,
        RecommendationResult result,
        CancellationToken cancellationToken) =>
        await CreateAsync(eventCenterName, result, null, cancellationToken).ConfigureAwait(false);

    public async Task<ReportDocument> CreateAsync(
        string eventCenterName,
        RecommendationResult result,
        PersonnelInfo? personnel,
        CancellationToken cancellationToken,
        string? cityName = null,
        DateOnly? taskStartDate = null,
        DateOnly? taskEndDate = null,
        string? countryName = null,
        IReadOnlyList<RequestedHotel>? requestedHotels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventCenterName);
        ArgumentNullException.ThrowIfNull(result);

        var recommendation = result.Recommendation
            ?? throw new InvalidOperationException("Belge yalnızca başarılı bir öneri için üretilebilir.");
        var venueLocation = result.VenueLocation
            ?? throw new InvalidOperationException("Başarılı sonuçta etkinlik alanı konumu bulunmalıdır.");

        var selectedCandidate = recommendation.Selected;
        var mapCandidates = recommendation.RankedHotels
            .OrderBy(candidate => candidate.TripCostEstimate?.TotalCost?.Amount ?? decimal.MaxValue)
            .ThenBy(candidate => candidate.Metrics.Kind)
            .ThenBy(candidate => candidate.Metrics.Kind == DistanceKind.Road ? candidate.Metrics.DurationSeconds : 0)
            .ThenBy(candidate => candidate.Metrics.DistanceMeters)
            .ThenBy(candidate => candidate.Hotel.Name, StringComparer.Ordinal)
            .ToArray();
        var mapWalkingRoutes = await GetWalkingRoutesAsync(mapCandidates, venueLocation, cancellationToken).ConfigureAwait(false);
        var selectedWalkingRoute = mapCandidates
            .Select((candidate, index) => (candidate, route: mapWalkingRoutes[index]))
            .Single(item => string.Equals(item.candidate.Hotel.Name, selectedCandidate.Hotel.Name, StringComparison.Ordinal)).route;
        var selectedName = recommendation.Selected.Hotel.Name;
        var rows = recommendation.RankedHotels
            .Select(candidate => string.Equals(candidate.Hotel.Name, selectedName, StringComparison.Ordinal)
                ? ToRow(candidate, selectedName, selectedWalkingRoute.Metrics, selectedWalkingRoute.Requested)
                : ToRow(candidate, selectedName, null, false))
            .ToArray();
        var explanationRows = rows;
        var map = await TryRenderMapAsync(venueLocation, mapCandidates, selectedName, mapWalkingRoutes, cancellationToken).ConfigureAwait(false);
        var selectedRow = rows.Single(row => row.IsSelected);
        var explanations = await explanationService.ExplainAsync(
            eventCenterName.Trim(),
            explanationRows,
            result.UnresolvedHotels,
            cancellationToken).ConfigureAwait(false);
        var transportExplanation = explanations.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation;
        if (selectedRow.TripCostEstimate?.TotalCost is null)
        {
            transportExplanation = "Görev toplam maliyeti, ulaşım kalemlerinden biri hesaplanamadığı için oluşturulamadı.";
        }
        var optionsForReport = new[]
        {
            new RecommendationReportOption(RecommendationStrategy.TransportPriority, selectedRow, transportExplanation),
        };

        var report = new RecommendationReport(
            options.DocumentTitle,
            eventCenterName.Trim(),
            selectedRow,
            rows,
            result.UnresolvedHotels,
            map,
            DateTimeOffset.UtcNow,
            personnel,
            transportExplanation,
            optionsForReport,
            result.PreferCityCenter,
            result.NearestAirport,
            cityName,
            taskStartDate,
            taskEndDate,
            countryName,
            BuildUnresolvedHotelDetails(result.UnresolvedHotels, requestedHotels));

        var document = await renderer.RenderAsync(report, cancellationToken).ConfigureAwait(false);

        if (document.Content.Length > options.MaxDocumentBytes)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Üretilen belge boyut sınırını aştı: {document.Content.Length} > {options.MaxDocumentBytes} bayt."));
        }

        return document;
    }

    private async Task<(RouteMetrics? Metrics, bool Requested)[]> GetWalkingRoutesAsync(
        IReadOnlyList<HotelCandidate> rankedHotels,
        GeoPoint venueLocation,
        CancellationToken cancellationToken)
    {
        var outcomes = await Task.WhenAll(rankedHotels.Select(async (candidate, index) =>
        {
            if (candidate.Metrics.Kind != DistanceKind.Road || candidate.Metrics.DistanceMeters >= 3000)
            {
                return (index, metrics: (RouteMetrics?)null, requested: false);
            }

            var route = await walkingRouteDistanceProvider
                .GetWalkingRouteAsync(candidate.Location, venueLocation, cancellationToken)
                .ConfigureAwait(false);
            return (index, metrics: route.Status == OutcomeStatus.Found ? route.Metrics : null, requested: true);
        })).ConfigureAwait(false);

        var result = new (RouteMetrics? Metrics, bool Requested)[rankedHotels.Count];
        foreach (var outcome in outcomes)
        {
            result[outcome.index] = (outcome.metrics, outcome.requested);
        }

        return result;
    }

    /// <summary>Görsel üretilemezse <c>null</c> döner; belge görselsiz üretilir (R-8).</summary>
    private async Task<StaticMapImage?> TryRenderMapAsync(
        GeoPoint venueLocation,
        IReadOnlyList<HotelCandidate> rankedHotels,
        string selectedName,
        IReadOnlyList<(RouteMetrics? Metrics, bool Requested)> walkingRoutes,
        CancellationToken cancellationToken)
    {
        var markers = rankedHotels
            .Select((candidate, index) =>
            {
                var walkingMetrics = walkingRoutes[index].Metrics;
                var mapMetrics = walkingMetrics ?? candidate.Metrics;
                return new StaticMapMarker(
                    candidate.Hotel.Name,
                    candidate.Location,
                    string.Equals(candidate.Hotel.Name, selectedName, StringComparison.Ordinal),
                    mapMetrics.Path,
                    walkingMetrics,
                    mapMetrics.PathSegments);
            })
            .ToArray();
        var request = new StaticMapRequest(venueLocation, markers, options.MapWidthPx, options.MapHeightPx);

        var outcome = await staticMapProvider.RenderAsync(request, cancellationToken).ConfigureAwait(false);

        return outcome.Status == OutcomeStatus.Found && outcome.Image is { IsEmpty: false } image
            ? new StaticMapImage(image, outcome.ContentType ?? "image/png", outcome.Attribution, outcome.IncludesDetailView)
            : null;
    }

    private static HotelReportRow ToRow(HotelCandidate candidate, string selectedName, RouteMetrics? walkingMetrics, bool walkingRouteRequested) =>
        new(
            candidate.Hotel.Name,
            candidate.Metrics.DistanceMeters,
            candidate.Metrics.DurationSeconds,
            candidate.Metrics.Kind,
            candidate.Hotel.Price,
            string.Equals(candidate.Hotel.Name, selectedName, StringComparison.Ordinal),
            candidate.Location,
            walkingMetrics,
            walkingRouteRequested,
            candidate.TaxiFareEstimate,
            candidate.TotalNightlyCost,
            candidate.TripCostEstimate);

    private static IReadOnlyList<UnresolvedHotelReportRow> BuildUnresolvedHotelDetails(
        IReadOnlyList<string> unresolvedNames,
        IReadOnlyList<RequestedHotel>? requestedHotels)
    {
        if (requestedHotels is null || unresolvedNames.Count == 0)
        {
            return [];
        }

        var unresolved = new HashSet<string>(unresolvedNames, StringComparer.OrdinalIgnoreCase);
        return requestedHotels
            .Where(hotel => !string.IsNullOrWhiteSpace(hotel.Name) && unresolved.Contains(hotel.Name.Trim()))
            .GroupBy(hotel => hotel.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var hotel = group.First();
                var price = hotel.Amount is >= 0 && NightlyPrice.IsValidCurrencyCode(hotel.Currency)
                    ? new NightlyPrice(hotel.Amount.Value, hotel.Currency!)
                    : null;
                return new UnresolvedHotelReportRow(hotel.Name!.Trim(), price);
            })
            .ToArray();
    }
}
