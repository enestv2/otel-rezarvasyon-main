using System.Net;
using Microsoft.Extensions.Options;
using System.Text.Json;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;
using RPAOtelRezervasyon.ProviderTests.Support;
using SkiaSharp;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class HostedStaticMapProviderTests
{
    [Fact]
    public async Task RenderAsync_does_not_synthesize_a_polyline_when_route_geometry_is_missing()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            var dimensions = RequestDimensions(request);
            var content = new ByteArrayContent(CreatePng(dimensions.Width * 2, dimensions.Height * 2));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));
        var venue = new GeoPoint(41, 29);
        var request = new StaticMapRequest(venue,
            [new StaticMapMarker("Geometrisiz otel", new GeoPoint(41.1, 29.1), false)], 640, 360);

        var outcome = await provider.RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Single(stub.RequestBodies);
        using var body = JsonDocument.Parse(stub.RequestBodies.Single()!);
        Assert.False(body.RootElement.TryGetProperty("geometries", out _));
        Assert.False(outcome.IncludesDetailView);
    }

    [Fact]
    public async Task RenderAsync_sends_all_locations_as_native_markers_and_auto_fits_for_hotel_count()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            var dimensions = RequestDimensions(request);
            var content = new ByteArrayContent(CreatePng(dimensions.Width * 2, dimensions.Height * 2));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));
        var multiHotelRequest = Request();
        var singleHotelRequest = new StaticMapRequest(multiHotelRequest.Venue,
            [multiHotelRequest.Markers[0]], multiHotelRequest.WidthPx, multiHotelRequest.HeightPx);

        Assert.Equal(OutcomeStatus.Found, (await provider.RenderAsync(singleHotelRequest, CancellationToken.None)).Status);
        Assert.Equal(OutcomeStatus.Found, (await provider.RenderAsync(multiHotelRequest, CancellationToken.None)).Status);

        Assert.Equal(2, stub.RequestBodies.Count);
        var requests = new[] { singleHotelRequest, multiHotelRequest };
        for (var requestIndex = 0; requestIndex < stub.RequestBodies.Count; requestIndex++)
        {
            using var body = JsonDocument.Parse(stub.RequestBodies[requestIndex]!);
            var root = body.RootElement;
            Assert.False(root.TryGetProperty("center", out _));
            Assert.False(root.TryGetProperty("zoom", out _));
            var markers = root.GetProperty("markers").EnumerateArray().ToArray();
            Assert.Equal(requests[requestIndex].Markers.Count + 1, markers.Length);
            Assert.Equal(requests[requestIndex].Venue.Latitude, markers[0].GetProperty("lat").GetDouble());
            Assert.Equal(requests[requestIndex].Venue.Longitude, markers[0].GetProperty("lon").GetDouble());
            for (var hotelIndex = 0; hotelIndex < requests[requestIndex].Markers.Count; hotelIndex++)
            {
                var hotel = requests[requestIndex].Markers[hotelIndex];
                var hotelMarker = markers[hotelIndex + 1];
                Assert.Equal(hotel.Location.Latitude, hotelMarker.GetProperty("lat").GetDouble());
                Assert.Equal(hotel.Location.Longitude, hotelMarker.GetProperty("lon").GetDouble());
                Assert.Equal("circle", hotelMarker.GetProperty("type").GetString());
                Assert.Equal((hotelIndex + 1).ToString(), hotelMarker.GetProperty("text").GetString());
                var expectedColor = StaticMapOverlay.RouteColor(hotel.IsSelected, hotelIndex);
                Assert.Equal($"#{expectedColor.Red:X2}{expectedColor.Green:X2}{expectedColor.Blue:X2}",
                    hotelMarker.GetProperty("color").GetString());
                Assert.Equal("#ffffff", hotelMarker.GetProperty("contentcolor").GetString());
                Assert.Equal("no", hotelMarker.GetProperty("whitecircle").GetString());
                Assert.Equal("no", hotelMarker.GetProperty("shadow").GetString());
            }
        }
    }

    [Fact]
    public async Task RenderAsync_requests_a_png_base_map_without_route_geometries_and_adds_markers()
    {
        var stub = new StubHttpMessageHandler(_ =>
        {
            var dimensions = RequestDimensions(_);
            var content = new ByteArrayContent(CreatePng(dimensions.Width * 2, dimensions.Height * 2));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal("image/png", outcome.ContentType);
        Assert.Equal("Powered by Geoapify", outcome.Attribution);
        Assert.False(outcome.IncludesDetailView);
        var uris = stub.RequestUris;
        Assert.Single(uris);
        Assert.All(stub.RequestMethods, method => Assert.Equal(HttpMethod.Post, method));
        Assert.All(uris, uri =>
        {
            Assert.Equal("maps.geoapify.com", uri.Host);
            Assert.Contains("apiKey=test-secret", uri.Query, StringComparison.Ordinal);
        });
        Assert.Equal("640x330", stub.RequestBodies
            .Select(body => JsonDocument.Parse(body!).RootElement)
            .Select(root => $"{root.GetProperty("width").GetInt32()}x{root.GetProperty("height").GetInt32()}")
            .Single());
        using var overviewBody = JsonDocument.Parse(stub.RequestBodies.Single()!);
        Assert.False(overviewBody.RootElement.TryGetProperty("geometries", out _));
        var overviewMarkers = overviewBody.RootElement.GetProperty("markers").EnumerateArray().ToArray();
        Assert.Equal(Request().Markers.Count + 1, overviewMarkers.Length);
        var venueMarker = overviewMarkers[0];
        Assert.Equal(Request().Venue.Latitude, venueMarker.GetProperty("lat").GetDouble());
        Assert.Equal(Request().Venue.Longitude, venueMarker.GetProperty("lon").GetDouble());
        Assert.Equal("awesome", venueMarker.GetProperty("type").GetString());
        Assert.Equal("map-marker-alt", venueMarker.GetProperty("icon").GetString());
        Assert.Equal("medium", venueMarker.GetProperty("iconsize").GetString());
        using var decoded = SKBitmap.Decode(outcome.Image!.Value.ToArray());
        Assert.Equal(1280, decoded.Width);
        Assert.Equal(720, decoded.Height);
        var pixels = Enumerable.Range(0, decoded.Width * decoded.Height)
            .Select(index => decoded.GetPixel(index % decoded.Width, index / decoded.Width))
            .ToArray();
        Assert.DoesNotContain(pixels, color => color == StaticMapOverlay.HotelColor(1));
        Assert.DoesNotContain(pixels, color => color == StaticMapOverlay.RouteColor(true, 0));
        Assert.DoesNotContain(pixels, color => color.Red > 180 && color.Green < 60 && color.Blue < 60);
        Assert.DoesNotContain(pixels, color => color == StaticMapOverlay.HotelColor(2));
    }

    [Fact]
    public async Task RenderAsync_returns_a_failure_when_the_only_full_width_map_request_fails()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.TransientError, outcome.Status);
        Assert.False(outcome.IncludesDetailView);
        Assert.Null(outcome.Image);
        Assert.Single(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_rejects_an_image_with_dimensions_that_do_not_match_the_full_width_request()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            var content = new ByteArrayContent(CreatePng(50, 50));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.ProviderError, outcome.Status);
        Assert.Null(outcome.Image);
        Assert.Single(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_without_an_api_key_does_not_call_the_service()
    {
        var stub = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Unexpected network call."));
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions()));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.ProviderError, outcome.Status);
        Assert.Empty(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_maps_http_failures_to_provider_outcomes_without_echoing_the_key()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "never-expose-this" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.TransientError, outcome.Status);
        Assert.Null(outcome.Image);
        Assert.Null(outcome.Attribution);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.White);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data!.ToArray();
    }

    private static (int Width, int Height) RequestDimensions(HttpRequestMessage request)
    {
        using var document = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = document.RootElement;
        return (root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32());
    }

    private static StaticMapRequest Request() => new(
        new GeoPoint(41.0082, 28.9784),
        [
            new StaticMapMarker("Otel", new GeoPoint(41.06, 28.987), true,
                [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.03, 28.98), new GeoPoint(41.06, 28.987)],
                new RouteMetrics(800, 500, DistanceKind.Road,
                    [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98), new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)],
                    [[new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98)], [new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)]]),
                [[new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98)], [new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)]]),
            new StaticMapMarker("Diğer otel", new GeoPoint(41.02, 28.95), false, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.015, 28.965), new GeoPoint(41.02, 28.95)], new RouteMetrics(400, 300, DistanceKind.Road, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.02, 28.95)])),
            new StaticMapMarker("Üçüncü otel", new GeoPoint(41.03, 29.01), false, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.02, 29.0), new GeoPoint(41.03, 29.01)], new RouteMetrics(500, 400, DistanceKind.Road, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.03, 29.01)])),
        ],
        640,
        360);
}
