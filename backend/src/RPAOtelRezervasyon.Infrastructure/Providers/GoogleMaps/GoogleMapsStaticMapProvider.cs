using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsStaticMapProvider(
    HttpClient httpClient,
    IOptions<GoogleMapsOptions> options) : IStaticMapProvider
{
    public const string Id = "google-maps-static";
    private const int ScaleFactor = 2;
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private readonly GoogleMapsOptions _options = options.Value;

    public string ProviderId => Id;

    public async Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Maps Static API supports base dimensions up to 640x640; scale=2 provides report resolution.
        if (request.WidthPx > 640 || request.HeightPx > 640)
        {
            return StaticMapOutcome.ProviderError(Id);
        }

        try
        {
            var overviewRequest = StaticMapComposition.CreateOverviewRequest(request);
            if (overviewRequest.HeightPx > 640)
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            var projection = StaticMapProjection.Fit(
                overviewRequest,
                _options.MaxZoom,
                paddingPx: 112,
                includeRouteGeometry: false);
            using var response = await httpClient.GetAsync(
                BuildUrl(overviewRequest, projection),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failure(response.StatusCode);
            }

            if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0
                || response.Content.Headers.ContentType?.MediaType is not ("image/png" or "image/jpeg"))
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            using var baseMap = SKBitmap.Decode(bytes);
            if (baseMap is null
                || baseMap.Width != overviewRequest.WidthPx * ScaleFactor
                || baseMap.Height != overviewRequest.HeightPx * ScaleFactor)
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            using var surface = SKSurface.Create(new SKImageInfo(
                baseMap.Width,
                baseMap.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul));
            if (surface is null)
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            surface.Canvas.DrawBitmap(baseMap, 0, 0, SKSamplingOptions.Default);
            surface.Canvas.Save();
            surface.Canvas.Scale(ScaleFactor);
            StaticMapOverlay.Draw(
                surface.Canvas,
                overviewRequest,
                point => projection.ToPixel(point, overviewRequest),
                StaticMapComposition.OverviewMarkerRanks(request),
                selectedRouteOnly: false,
                drawRoutes: false,
                selectedMarkerScale: (float)Math.Clamp(0.9 + ((projection.Zoom - 10) * 0.06), 1, 1.5));
            surface.Canvas.Restore();

            using var snapshot = surface.Snapshot();
            using var overview = SKBitmap.FromImage(snapshot);
            var composed = StaticMapComposition.ComposePng(
                overview,
                StaticMapComposition.HeaderHeight(request.HeightPx),
                ScaleFactor);
            return StaticMapOutcome.Found(Id, composed, "image/png", "Google Maps", includesDetailView: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return StaticMapOutcome.TransientError(Id);
        }
        catch (HttpRequestException)
        {
            return StaticMapOutcome.TransientError(Id);
        }
        catch (Exception)
        {
            return StaticMapOutcome.ProviderError(Id);
        }
    }

    private string BuildUrl(StaticMapRequest request, StaticMapProjection projection)
    {
        var latitude = projection.CenterLatitude.ToString("F6", CultureInfo.InvariantCulture);
        var longitude = projection.CenterLongitude.ToString("F6", CultureInfo.InvariantCulture);
        var parameters = new List<string>
        {
            $"center={latitude},{longitude}",
            // StaticMapProjection uses a 512px world; Google Static Maps uses the equivalent 256px world.
            $"zoom={(int)Math.Round(projection.Zoom, MidpointRounding.AwayFromZero) + 1}",
            $"size={request.WidthPx}x{request.HeightPx}",
            $"scale={ScaleFactor}",
            "maptype=roadmap",
            $"language={Uri.EscapeDataString(_options.LanguageCode)}",
            $"key={Uri.EscapeDataString(_options.ApiKey)}",
        };
        if (!string.IsNullOrWhiteSpace(_options.RegionCode))
        {
            parameters.Add($"region={Uri.EscapeDataString(_options.RegionCode)}");
        }

        return $"{_options.StaticMapBaseUrl}?{string.Join('&', parameters)}";
    }

    private static StaticMapOutcome Failure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500
            ? StaticMapOutcome.TransientError(Id)
            : StaticMapOutcome.ProviderError(Id);

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > MaxResponseBytes)
            {
                return null;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
