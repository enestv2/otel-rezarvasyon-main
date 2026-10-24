using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required, Url]
    public string GeocodingBaseUrl { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";

    [Required, Url]
    public string PlacesBaseUrl { get; set; } = "https://places.googleapis.com/v1";

    [Required, Url]
    public string RoutesBaseUrl { get; set; } = "https://routes.googleapis.com/directions/v2:computeRoutes";

    [Required, Url]
    public string StaticMapBaseUrl { get; set; } = "https://maps.googleapis.com/maps/api/staticmap";

    [Required]
    public string LanguageCode { get; set; } = "tr";

    public string RegionCode { get; set; } = string.Empty;

    [Range(0.1, 20)]
    public double RequestsPerSecond { get; set; } = 5;

    [Range(1, 5)]
    public int RetryMaxAttempts { get; set; } = 2;

    [Range(1000, 50000)]
    public int AirportSearchRadiusMeters { get; set; } = 50000;

    [Range(1, 20)]
    public int MaxZoom { get; set; } = 18;
}
