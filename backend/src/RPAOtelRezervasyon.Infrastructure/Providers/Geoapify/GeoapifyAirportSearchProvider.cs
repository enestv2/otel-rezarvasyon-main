using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

public sealed class GeoapifyAirportSearchProvider(
    HttpClient httpClient,
    IOptions<GeoapifyOptions> options) : IAirportSearchProvider
{
    public const string Id = "geoapify-airport-search";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AirportCandidate?> FindNearestAirportAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var airports = await FindNearbyAirportsAsync(location, cancellationToken).ConfigureAwait(false);
        return airports.FirstOrDefault();
    }

    public async Task<IReadOnlyList<AirportCandidate>> FindNearbyAirportsAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);

        var bias = string.Create(CultureInfo.InvariantCulture,
            $"proximity:{location.Longitude},{location.Latitude}");
        var query = $"v2/places?categories=airport&bias={Uri.EscapeDataString(bias)}&limit=50&lang=tr&apiKey={Uri.EscapeDataString(options.Value.ApiKey)}";

        try
        {
            using var response = await httpClient.GetAsync(query, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<PlacesResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return payload?.Features?
                .Where(item => item.Properties is not null
                    && item.Geometry?.Coordinates is { Length: >= 2 }
                    && !string.IsNullOrWhiteSpace(item.Properties.Name)
                    && item.Properties.Distance is >= 0
                    && IsPublicAirport(item.Properties))
                .OrderBy(item => item.Properties!.Distance)
                .Select(item =>
                {
                    var properties = item.Properties!;
                    var coordinates = item.Geometry!.Coordinates!;
                    var longitude = coordinates[0];
                    var latitude = coordinates[1];
                    return double.IsFinite(latitude) && latitude is >= -90 and <= 90
                        && double.IsFinite(longitude) && longitude is >= -180 and <= 180
                        ? new AirportCandidate(properties.Name!, new GeoPoint(latitude, longitude), properties.Distance!.Value)
                        : null;
                })
                .Where(airport => airport is not null)
                .Select(airport => airport!)
                .ToArray() ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or IOException or JsonException)
        {
            return [];
        }
    }

    private static bool IsPublicAirport(PlaceProperties properties)
    {
        var categories = properties.Categories ?? [];
        if (categories.Contains("airport.military", StringComparer.OrdinalIgnoreCase)
            || categories.Contains("airport.private", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedName = NormalizeName(properties.Name!);
        return !normalizedName.Contains("askeri", StringComparison.Ordinal)
            && !normalizedName.Contains("military", StringComparison.Ordinal)
            && !normalizedName.Contains("air base", StringComparison.Ordinal)
            && !normalizedName.Contains("airbase", StringComparison.Ordinal)
            && !normalizedName.Contains("hava ussu", StringComparison.Ordinal)
            && !normalizedName.EndsWith("ussu", StringComparison.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record PlacesResponse(IReadOnlyList<PlaceFeature>? Features);
    private sealed record PlaceFeature(PlaceProperties? Properties, PlaceGeometry? Geometry);
    private sealed record PlaceProperties(string? Name, int? Distance, IReadOnlyList<string>? Categories);
    private sealed record PlaceGeometry(double[]? Coordinates);
}
