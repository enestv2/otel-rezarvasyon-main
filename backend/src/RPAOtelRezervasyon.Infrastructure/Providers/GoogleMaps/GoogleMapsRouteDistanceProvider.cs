using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Http;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsRouteDistanceProvider(
    HttpClient httpClient,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleMapsRouteDistanceProvider> logger) : IRouteDistanceProvider, IWalkingRouteDistanceProvider
{
    public const string Id = "google-maps";
    private const int MaxPolylinePoints = 512;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public string ProviderId => Id;

    public Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken) =>
        GetRouteAsync(origin, destination, "DRIVE", cancellationToken);

    public Task<RouteOutcome> GetWalkingRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken) =>
        GetRouteAsync(origin, destination, "WALK", cancellationToken);

    private async Task<RouteOutcome> GetRouteAsync(
        GeoPoint origin,
        GeoPoint destination,
        string travelMode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        var body = new Dictionary<string, object>
        {
            ["origin"] = new { location = new { latLng = new { latitude = origin.Latitude, longitude = origin.Longitude } } },
            ["destination"] = new { location = new { latLng = new { latitude = destination.Latitude, longitude = destination.Longitude } } },
            ["travelMode"] = travelMode,
            ["languageCode"] = options.Value.LanguageCode,
            ["units"] = "METRIC",
            ["polylineQuality"] = "OVERVIEW",
        };
        if (travelMode == "DRIVE")
        {
            body["routingPreference"] = "TRAFFIC_UNAWARE";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, options.Value.RoutesBaseUrl)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask",
            "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline");

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
                    logger.LogWarning("Google Routes API yanıtı başarısız: {StatusCode}.", (int)response.StatusCode);
                    return ProviderHttpStatus.IsTransient(response.StatusCode)
                        ? RouteOutcome.TransientError()
                        : RouteOutcome.ProviderError();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var payload = await JsonSerializer.DeserializeAsync<RoutesResponse>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                var route = payload?.Routes?.FirstOrDefault();
                if (route is null)
                {
                    return RouteOutcome.NotFound();
                }

                if (route.DistanceMeters < 0 || !double.IsFinite(route.DistanceMeters) || route.DistanceMeters > int.MaxValue
                    || !TryParseDuration(route.Duration, out var durationSeconds))
                {
                    return RouteOutcome.ProviderError();
                }

                var distance = (int)Math.Round(route.DistanceMeters, MidpointRounding.AwayFromZero);
                var path = DecodePolyline(route.Polyline?.EncodedPolyline);
                return RouteOutcome.Found(new RouteMetrics(distance, durationSeconds, DistanceKind.Road, path));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Google Routes API zaman aşımına uğradı.");
            return RouteOutcome.TransientError();
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Google Routes API isteği başarısız oldu.");
            return RouteOutcome.TransientError();
        }
        catch (JsonException)
        {
            logger.LogWarning("Google Routes API yanıtı beklenen şemaya uymuyor.");
            return RouteOutcome.TransientError();
        }
        catch (FormatException)
        {
            logger.LogWarning("Google Routes API süre alanı çözümlenemedi.");
            return RouteOutcome.ProviderError();
        }
        catch (OverflowException)
        {
            logger.LogWarning("Google Routes API rota değeri beklenen aralığın dışında.");
            return RouteOutcome.ProviderError();
        }
    }

    private static bool TryParseDuration(string? duration, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(duration) || !duration.EndsWith('s'))
        {
            return false;
        }

        if (!double.TryParse(duration[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value) || value < 0 || value > int.MaxValue)
        {
            return false;
        }

        seconds = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return true;
    }

    private static IReadOnlyList<GeoPoint> DecodePolyline(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return [];
        }

        var points = new List<GeoPoint>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;
        while (index < encoded.Length)
        {
            latitude += DecodeValue(encoded, ref index);
            longitude += DecodeValue(encoded, ref index);
            var decodedLatitude = latitude / 1e5;
            var decodedLongitude = longitude / 1e5;
            if (double.IsFinite(decodedLatitude) && decodedLatitude is >= -90 and <= 90
                && double.IsFinite(decodedLongitude) && decodedLongitude is >= -180 and <= 180)
            {
                points.Add(new GeoPoint(decodedLatitude, decodedLongitude));
            }
        }

        if (points.Count <= MaxPolylinePoints)
        {
            return points;
        }

        var sampled = new GeoPoint[MaxPolylinePoints];
        for (var sample = 0; sample < sampled.Length; sample++)
        {
            var pointIndex = (int)Math.Round(sample * (points.Count - 1d) / (sampled.Length - 1), MidpointRounding.AwayFromZero);
            sampled[sample] = points[pointIndex];
        }

        return sampled;
    }

    private static int DecodeValue(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;
        do
        {
            if (index >= encoded.Length || shift > 30)
            {
                throw new FormatException("Encoded polyline değeri geçersiz.");
            }

            chunk = encoded[index++] - 63;
            if (chunk is < 0 or > 63)
            {
                throw new FormatException("Encoded polyline karakteri geçersiz.");
            }

            result |= (chunk & 0x1f) << shift;
            shift += 5;
        } while (chunk >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    private sealed record RoutesResponse(IReadOnlyList<RouteResult>? Routes);
    private sealed record RouteResult(double DistanceMeters, string? Duration, RoutePolyline? Polyline);
    private sealed record RoutePolyline(string? EncodedPolyline);
}
