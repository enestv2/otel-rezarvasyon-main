using RPAOtelRezervasyon.Application.Modules.Delivery;
using RPAOtelRezervasyon.Application.Modules.Airports;
using RPAOtelRezervasyon.Application.Modules.DistanceRouting;
using RPAOtelRezervasyon.Application.Modules.HotelGeocoding;
using RPAOtelRezervasyon.Application.Modules.Ranking;
using RPAOtelRezervasyon.Application.Modules.TaxiFares;
using RPAOtelRezervasyon.Application.Modules.VenueGeocoding;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;

namespace RPAOtelRezervasyon.Application.Orchestration;

/// <summary>
/// Uçtan uca akış: doğrula → otel adlarını tekilleştir → etkinlik alanını çözümle → otelleri
/// çözümle (paralel) → mesafeleri çöz (paralel) → sırala/seç → teslim et.
/// Kısmi hata yalıtılır: çözümlenemeyen/ölçülemeyen oteller sonucu düşürmez (BR-1).
/// </summary>
public sealed class HotelRecommendationOrchestrator(
    VenueGeocodingService venueGeocodingService,
    HotelGeocodingService hotelGeocodingService,
    DistanceRoutingService distanceRoutingService,
    RankingService rankingService,
    RecommendationDeliveryService deliveryService,
    TaxiFareService? taxiFareService = null,
    AirportSearchService? airportSearchService = null)
{
    public async Task<RecommendationResult> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = RecommendationRequestValidator.Validate(
            request.EventCenterName,
            request.Hotels,
            request.Personnel,
            request.GeocodingContext.City,
            request.GeocodingContext.Country);
        if (!validation.IsValid)
        {
            return RecommendationResult.Invalid(validation);
        }

        if (request.TaskStartDate.HasValue != request.TaskEndDate.HasValue
            || (request.TaskStartDate is { } startDate && request.TaskEndDate is { } endDate && endDate < startDate))
        {
            return RecommendationResult.Invalid(new ValidationResult([
                new ValidationIssue("task-date-range-invalid", "Görev başlangıç ve bitiş tarihleri birlikte verilmeli; bitiş tarihi başlangıçtan önce olmamalıdır."),
            ]));
        }

        var eventCenterName = request.EventCenterName.Trim();

        var hotels = ContractedHotel.DistinctByName(
            request.Hotels.Select(ToContractedHotel));

        var currencies = hotels.Select(hotel => hotel.Price.Currency).Distinct(StringComparer.Ordinal).ToArray();
        if (taxiFareService is not null && currencies.Length != 1)
        {
            return RecommendationResult.Invalid(new ValidationResult([
                new ValidationIssue("hotel-currency-mixed", "Ulaşım maliyetiyle toplam gecelik maliyet hesaplamak için tüm otel fiyatları aynı para biriminde olmalıdır."),
            ]));
        }

        var eventCenterOutcome = await venueGeocodingService
            .ResolveAsync(eventCenterName, request.GeocodingContext, cancellationToken)
            .ConfigureAwait(false);

        if (eventCenterOutcome.Status is OutcomeStatus.TransientError or OutcomeStatus.ProviderError)
        {
            return RecommendationResult.ProviderUnavailable(eventCenterName);
        }

        if (eventCenterOutcome.Status != OutcomeStatus.Found || eventCenterOutcome.Location is null)
        {
            return RecommendationResult.VenueNotResolved(eventCenterName);
        }

        var eventCenterLocation = eventCenterOutcome.Location;
        var targetLocation = eventCenterLocation;
        if (request.PreferCityCenter)
        {
            var cityOutcome = await venueGeocodingService
                .ResolveAsync(request.GeocodingContext.City!, new GeocodingContext(null, request.GeocodingContext.Country), cancellationToken)
                .ConfigureAwait(false);
            if (cityOutcome.Status is OutcomeStatus.TransientError or OutcomeStatus.ProviderError)
            {
                return RecommendationResult.ProviderUnavailable(eventCenterName);
            }

            if (cityOutcome.Status != OutcomeStatus.Found || cityOutcome.Location is null)
            {
                return RecommendationResult.VenueNotResolved(eventCenterName);
            }

            targetLocation = cityOutcome.Location;
        }

        var hotelGeocoding = await hotelGeocodingService
            .ResolveAsync(hotels, new GeocodingContext(null, request.GeocodingContext.Country), cancellationToken)
            .ConfigureAwait(false);

        var routeTasks = hotelGeocoding.Resolved.Select(async resolved =>
        {
            var targetMetricsTask = distanceRoutingService.ResolveMetricsAsync(resolved.Location, targetLocation, cancellationToken);
            var eventCenterMetricsTask = targetLocation == eventCenterLocation
                ? targetMetricsTask
                : distanceRoutingService.ResolveMetricsAsync(resolved.Location, eventCenterLocation, cancellationToken);
            await Task.WhenAll(targetMetricsTask, eventCenterMetricsTask).ConfigureAwait(false);
            return (Target: await targetMetricsTask.ConfigureAwait(false), EventCenter: await eventCenterMetricsTask.ConfigureAwait(false));
        }).ToArray();
        var metrics = await Task.WhenAll(routeTasks).ConfigureAwait(false);

        var candidates = new List<HotelCandidate>();
        var unresolvedNames = new List<string>();

        foreach (var hotel in hotelGeocoding.Unresolved)
        {
            unresolvedNames.Add(hotel.Name);
        }

        for (var index = 0; index < hotelGeocoding.Resolved.Count; index++)
        {
            var resolved = hotelGeocoding.Resolved[index];
            var routeMetrics = metrics[index].Target;
            var eventCenterMetrics = metrics[index].EventCenter;

            if (routeMetrics is null || (taxiFareService is not null && eventCenterMetrics is null))
            {
                unresolvedNames.Add(resolved.Hotel.Name);
                continue;
            }

            TaxiFareEstimate? taxiFareEstimate = null;
            if (taxiFareService is not null)
            {
                taxiFareEstimate = await taxiFareService.EstimateRoundTripAsync(
                    request.GeocodingContext.City!,
                    request.GeocodingContext.Country,
                    resolved.Hotel.Price.Currency,
                    eventCenterMetrics!,
                    cancellationToken).ConfigureAwait(false);
                if (taxiFareEstimate is null)
                {
                    return RecommendationResult.Invalid(new ValidationResult([
                        new ValidationIssue("taxi-fare-currency-unavailable", "Taksi tarifesi para birimi otel fiyatlarıyla eşleşmedi; toplam gecelik maliyet hesaplanamadı."),
                    ]));
                }
            }

            candidates.Add(new HotelCandidate(resolved.Hotel, resolved.Location, routeMetrics, taxiFareEstimate));
        }

        if (candidates.Count == 0)
        {
            return RecommendationResult.NoResolvedHotels(unresolvedNames, eventCenterName);
        }

        var stayNights = request.TaskStartDate is { } taskStart && request.TaskEndDate is { } taskEnd
            ? taskEnd.DayNumber - taskStart.DayNumber
            : 1;
        var commuteDays = Math.Max(1, stayNights);
        var candidatesWithTripCosts = await Task.WhenAll(candidates.Select(async candidate =>
        {
            var airport = airportSearchService is null
                ? null
                : await airportSearchService.FindNearestAsync(candidate.Location, cancellationToken).ConfigureAwait(false);
            if (airport is not null && taxiFareService is not null)
            {
                var airportFare = await taxiFareService.EstimateRoundTripAsync(
                    request.GeocodingContext.City!,
                    request.GeocodingContext.Country,
                    candidate.Hotel.Price.Currency,
                    airport.Route,
                    cancellationToken).ConfigureAwait(false);
                airport = airport with { AirportHotelRoundTripFare = airportFare };
            }

            var tripCost = TripCostEstimate.Calculate(
                candidate.Hotel.Price,
                candidate.TaxiFareEstimate,
                airport?.AirportHotelRoundTripFare,
                stayNights,
                commuteDays);

            return new HotelCandidate(candidate.Hotel, candidate.Location, candidate.Metrics,
                candidate.TaxiFareEstimate, airport, tripCost);
        })).ConfigureAwait(false);

        var recommendation = rankingService.Select(candidatesWithTripCosts);
        var nearestAirport = recommendation.Selected.NearestAirport;

        return deliveryService.Deliver(eventCenterName, targetLocation, recommendation, unresolvedNames,
            request.PreferCityCenter, nearestAirport);
    }

    /// <summary>
    /// Doğrulama geçtikten sonra çağrılır (RecommendationRequestValidator): ad, tutar ve para birimi
    /// bu noktada geçerlidir. Fiyat yalnızca otelle birlikte taşınır; sıralamaya katılmaz (R-4).
    /// </summary>
    private static ContractedHotel ToContractedHotel(RequestedHotel requested) =>
        new(requested.Name!.Trim(), new NightlyPrice(requested.Amount!.Value, requested.Currency!));
}
