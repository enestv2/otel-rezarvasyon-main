using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsAirportSearchProvider(
    HttpClient httpClient,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleMapsAirportSearchProvider> logger) : IAirportSearchProvider
{
    public const string Id = "google-maps";
    private const double EarthRadiusMeters = 6_371_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AirportCandidate?> FindNearestAirportAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var airports = await FindNearbyAirportsAsync(location, cancellationToken).ConfigureAwait(false);
        return airports.FirstOrDefault();
    }

    public async Task<IReadOnlyList<AirportCandidate>> FindNearbyAirportsAsync(
        GeoPoint location,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);

        var body = new Dictionary<string, object>
        {
            ["includedTypes"] = new[] { "airport" },
            ["maxResultCount"] = 20,
            ["rankPreference"] = "DISTANCE",
            ["languageCode"] = options.Value.LanguageCode,
            ["locationRestriction"] = new
            {
                circle = new
                {
                    center = new { latitude = location.Latitude, longitude = location.Longitude },
                    radius = (double)options.Value.AirportSearchRadiusMeters,
                },
            },
        };
        if (!string.IsNullOrWhiteSpace(options.Value.RegionCode))
        {
            body["regionCode"] = options.Value.RegionCode;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{options.Value.PlacesBaseUrl.TrimEnd('/')}/places:searchNearby")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask",
            "places.displayName,places.location,places.formattedAddress,places.types");

        try
        {
            using (request)
            using (var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Google Places havalimanı araması başarısız: {StatusCode}.", (int)response.StatusCode);
                    return [];
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var payload = await JsonSerializer.DeserializeAsync<PlacesResponse>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                return payload?.Places?
                    .Where(IsEligibleAirport)
                    .Select(place => new AirportCandidate(
                        place.DisplayName!.Text!,
                        new GeoPoint(place.Location!.Latitude, place.Location.Longitude),
                        (int)Math.Round(HaversineMeters(location, new GeoPoint(place.Location.Latitude, place.Location.Longitude)), MidpointRounding.AwayFromZero)))
                    .OrderBy(airport => airport.StraightLineDistanceMeters)
                    .ToArray() ?? [];
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Google Places havalimanı araması zaman aşımına uğradı.");
            return [];
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Google Places havalimanı araması isteği başarısız oldu.");
            return [];
        }
        catch (JsonException)
        {
            logger.LogWarning("Google Places havalimanı yanıtı beklenen şemaya uymuyor.");
            return [];
        }
        catch (OverflowException)
        {
            logger.LogWarning("Google Places havalimanı koordinatı geçerli aralığın dışında.");
            return [];
        }
    }

    private static bool IsEligibleAirport(PlaceResult place)
    {
        if (string.IsNullOrWhiteSpace(place.DisplayName?.Text)
            || place.Location is not { } location
            || !double.IsFinite(location.Latitude) || location.Latitude is < -90 or > 90
            || !double.IsFinite(location.Longitude) || location.Longitude is < -180 or > 180)
        {
            return false;
        }

        var types = place.Types ?? [];
        if (types.Any(type => type.Contains("military", StringComparison.OrdinalIgnoreCase)
            || type.Contains("private", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var normalizedName = GoogleMapsGeocodingProvider.NormalizeName(place.DisplayName.Text);
        return !normalizedName.Contains("military", StringComparison.Ordinal)
            && !normalizedName.Contains("air force", StringComparison.Ordinal)
            && !normalizedName.Contains("air base", StringComparison.Ordinal)
            && !normalizedName.Contains("airbase", StringComparison.Ordinal)
            && !normalizedName.Contains("private", StringComparison.Ordinal)
            && !normalizedName.Contains("askeri", StringComparison.Ordinal)
            && !normalizedName.Contains("hava ussu", StringComparison.Ordinal);
    }

    private static double HaversineMeters(GeoPoint first, GeoPoint second)
    {
        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(ToRadians(first.Latitude)) * Math.Cos(ToRadians(second.Latitude))
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return 2 * EarthRadiusMeters * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private sealed record PlacesResponse(IReadOnlyList<PlaceResult>? Places);
    private sealed record PlaceResult(PlaceDisplayName? DisplayName, PlaceLocation? Location, IReadOnlyList<string>? Types);
    private sealed record PlaceDisplayName(string? Text);
    private sealed record PlaceLocation(double Latitude, double Longitude);
}
