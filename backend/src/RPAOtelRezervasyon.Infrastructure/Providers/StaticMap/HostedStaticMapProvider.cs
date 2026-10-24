using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

public sealed class HostedStaticMapProvider(HttpClient httpClient, IOptions<StaticMapOptions> options) : IStaticMapProvider
{
    public const string Id = "geoapify";
    private const int ScaleFactor = 2;
    private const int MaxResponseBytes = 8 * 1024 * 1024;
    private readonly StaticMapOptions _options = options.Value;

    public string ProviderId => Id;

    public async Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return StaticMapOutcome.ProviderError(Id);
        }

        try
        {
            var overviewRequest = StaticMapComposition.CreateOverviewRequest(request);
            var projection = StaticMapProjection.Fit(overviewRequest, _options.MaxZoom, paddingPx: 112,
                includeRouteGeometry: false);
            using var overviewResult = await FetchBaseMapAsync(overviewRequest, projection, cancellationToken);
            if (overviewResult.Bitmap is null)
            {
                return Failure(overviewResult.Failure ?? OutcomeStatus.ProviderError);
            }

            using var overview = RenderView(overviewResult.Bitmap,
                overviewRequest,
                projection,
                StaticMapComposition.OverviewMarkerRanks(request), selectedRouteOnly: false, drawRoutes: false);
            if (overview is null)
            {
                return StaticMapOutcome.ProviderError(Id);
            }

            var composed = StaticMapComposition.ComposePng(overview, StaticMapComposition.HeaderHeight(request.HeightPx), ScaleFactor);
            return StaticMapOutcome.Found(Id, composed, "image/png", _options.Attribution, includesDetailView: false);
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

    private async Task<MapBitmapResult> FetchBaseMapAsync(StaticMapRequest request,
        StaticMapProjection projection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = BuildRequest(request, projection);
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500
                    ? OutcomeStatus.TransientError
                    : OutcomeStatus.ProviderError;
                return new MapBitmapResult(null, status);
            }

            if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            {
                return new MapBitmapResult(null, OutcomeStatus.ProviderError);
            }

            var bytes = await ReadBoundedAsync(response.Content, cancellationToken);
            if (bytes is null || bytes.Length == 0 ||
                response.Content.Headers.ContentType?.MediaType is not ("image/png" or "image/jpeg"))
            {
                return new MapBitmapResult(null, OutcomeStatus.ProviderError);
            }

            var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null || bitmap.Width != request.WidthPx * ScaleFactor || bitmap.Height != request.HeightPx * ScaleFactor)
            {
                bitmap?.Dispose();
                return new MapBitmapResult(null, OutcomeStatus.ProviderError);
            }

            return new MapBitmapResult(bitmap, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new MapBitmapResult(null, OutcomeStatus.TransientError);
        }
        catch (HttpRequestException)
        {
            return new MapBitmapResult(null, OutcomeStatus.TransientError);
        }
        catch (Exception)
        {
            return new MapBitmapResult(null, OutcomeStatus.ProviderError);
        }
    }

    private static SKBitmap? RenderView(SKBitmap baseMap, StaticMapRequest request,
        StaticMapProjection projection,
        IReadOnlyList<int> markerRanks, bool selectedRouteOnly = false, bool drawRoutes = true)
    {
        using var surface = SKSurface.Create(new SKImageInfo(baseMap.Width, baseMap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
        {
            return null;
        }

        surface.Canvas.DrawBitmap(baseMap, 0, 0, SKSamplingOptions.Default);
        surface.Canvas.Save();
        surface.Canvas.Scale(ScaleFactor);
        StaticMapOverlay.Draw(surface.Canvas, request, point => projection.ToPixel(point, request), markerRanks,
            selectedRouteOnly, drawRoutes, selectedMarkerScale: SelectedMarkerScale(projection.Zoom));
        surface.Canvas.Restore();
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static StaticMapOutcome Failure(OutcomeStatus status) =>
        status == OutcomeStatus.TransientError ? StaticMapOutcome.TransientError(Id) : StaticMapOutcome.ProviderError(Id);

    private static float SelectedMarkerScale(double zoom) => (float)Math.Clamp(0.9 + ((zoom - 10) * 0.06), 1, 1.5);

    private sealed record MapBitmapResult(SKBitmap? Bitmap, OutcomeStatus? Failure) : IDisposable
    {
        public void Dispose() => Bitmap?.Dispose();
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaxResponseBytes)
            {
                return null;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private HttpRequestMessage BuildRequest(StaticMapRequest request, StaticMapProjection projection)
    {
        var uri = new Uri($"{_options.BaseUrl}?apiKey={Uri.EscapeDataString(_options.ApiKey)}", UriKind.Absolute);
        var body = new Dictionary<string, object>
        {
            ["style"] = _options.Style,
            ["width"] = request.WidthPx,
            ["height"] = request.HeightPx,
            ["format"] = "png",
            ["scaleFactor"] = ScaleFactor,
            ["attribution"] = "mandatory",
            ["center"] = new { lat = projection.CenterLatitude, lon = projection.CenterLongitude },
            ["zoom"] = projection.Zoom,
        };
        return new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
    }
}
