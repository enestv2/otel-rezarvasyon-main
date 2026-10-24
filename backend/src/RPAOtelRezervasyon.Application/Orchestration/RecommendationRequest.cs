using RPAOtelRezervasyon.Domain.Validation;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Orchestration;

/// <summary>Servise gelen ham istek: etkinlik alanı adı + fiyatlı anlaşmalı otel girdileri (R-1).</summary>
public sealed record RecommendationRequest
{
    public string EventCenterName { get; }

    public IReadOnlyList<RequestedHotel> Hotels { get; }

    public PersonnelInfo Personnel { get; }

    public GeocodingContext GeocodingContext { get; }

    public bool PreferCityCenter { get; }

    public DateOnly? TaskStartDate { get; }

    public DateOnly? TaskEndDate { get; }

    public RecommendationRequest(
        string eventCenterName,
        IReadOnlyList<RequestedHotel> hotels,
        PersonnelInfo? personnel = null,
        GeocodingContext? geocodingContext = null,
        bool preferCityCenter = false,
        DateOnly? taskStartDate = null,
        DateOnly? taskEndDate = null)
    {
        ArgumentNullException.ThrowIfNull(eventCenterName);
        ArgumentNullException.ThrowIfNull(hotels);

        EventCenterName = eventCenterName;
        Hotels = hotels;
        Personnel = personnel ?? new PersonnelInfo(null, null, null);
        GeocodingContext = geocodingContext ?? global::RPAOtelRezervasyon.Domain.Models.GeocodingContext.Empty;
        PreferCityCenter = preferCityCenter;
        TaskStartDate = taskStartDate;
        TaskEndDate = taskEndDate;
    }
}
