using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Http;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsGeocodingProvider(
    HttpClient httpClient,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleMapsGeocodingProvider> logger) : IGeocodingProvider
{
    public const string Id = "google-maps";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> GenericPlaceTokens = new(StringComparer.Ordinal)
    {
        "airport", "arena", "center", "centre", "convention", "event", "hotel", "international",
        "resort", "stadium", "venue", "building", "north", "south", "east", "west",
    };

    public string ProviderId => Id;

    public Task<GeocodingOutcome> GeocodeAsync(string placeName, CancellationToken cancellationToken) =>
        GeocodeAsync(placeName, GeocodingContext.Empty, cancellationToken);

    public async Task<GeocodingOutcome> GeocodeAsync(
        string placeName,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeName);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            return string.IsNullOrWhiteSpace(context.City)
                ? await GeocodeAddressAsync(placeName, context, cancellationToken).ConfigureAwait(false)
                : await SearchPlaceAsync(placeName, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Google Maps geocoding zaman aşımına uğradı.");
            return GeocodingOutcome.TransientError(Id);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Google Maps geocoding isteği başarısız oldu.");
            return GeocodingOutcome.TransientError(Id);
        }
        catch (JsonException)
        {
            logger.LogWarning("Google Maps geocoding yanıtı beklenen şemaya uymuyor.");
            return GeocodingOutcome.TransientError(Id);
        }
    }

    private async Task<GeocodingOutcome> GeocodeAddressAsync(
        string placeName,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        var address = string.Join(", ", new[] { placeName.Trim(), context.Country?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var query = $"address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(options.Value.ApiKey)}"
            + $"&language={Uri.EscapeDataString(options.Value.LanguageCode)}";
        if (!string.IsNullOrWhiteSpace(options.Value.RegionCode))
        {
            query += $"&region={Uri.EscapeDataString(options.Value.RegionCode)}";
        }

        using var response = await httpClient.GetAsync(
            $"{options.Value.GeocodingBaseUrl}?{query}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return ProviderHttpStatus.IsTransient(response.StatusCode)
                ? GeocodingOutcome.TransientError(Id)
                : GeocodingOutcome.ProviderError(Id);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GeocodingResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (payload?.Status == "ZERO_RESULTS")
        {
            return GeocodingOutcome.NotFound(Id);
        }

        if (payload?.Status != "OK")
        {
            logger.LogWarning("Google Geocoding API beklenmeyen durum döndürdü: {Status}.", payload?.Status);
            return payload?.Status is "OVER_QUERY_LIMIT" or "UNKNOWN_ERROR"
                ? GeocodingOutcome.TransientError(Id)
                : GeocodingOutcome.ProviderError(Id);
        }

        var match = payload.Results?.FirstOrDefault(result => IsValid(result.Geometry?.Location));
        return match?.Geometry?.Location is { } location
            ? GeocodingOutcome.Found(Id, new GeoPoint(location.Latitude, location.Longitude), match.FormattedAddress)
            : GeocodingOutcome.NotFound(Id);
    }

    private async Task<GeocodingOutcome> SearchPlaceAsync(
        string placeName,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        var query = string.Join(", ", new[] { placeName.Trim(), context.City?.Trim(), context.Country?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var body = new Dictionary<string, object>
        {
            ["textQuery"] = query,
            ["languageCode"] = options.Value.LanguageCode,
            ["maxResultCount"] = 10,
        };
        if (!string.IsNullOrWhiteSpace(options.Value.RegionCode))
        {
            body["regionCode"] = options.Value.RegionCode;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{options.Value.PlacesBaseUrl.TrimEnd('/')}/places:searchText")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask",
            "places.displayName,places.formattedAddress,places.location");

        using (request)
        using (var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
            {
                return ProviderHttpStatus.IsTransient(response.StatusCode)
                    ? GeocodingOutcome.TransientError(Id)
                    : GeocodingOutcome.ProviderError(Id);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<PlacesResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var match = payload?.Places?.FirstOrDefault(place =>
                IsValid(place.Location) && MatchesPlaceName(placeName, context, place));
            return match?.Location is { } location
                ? GeocodingOutcome.Found(Id, new GeoPoint(location.Latitude, location.Longitude), match.FormattedAddress)
                : GeocodingOutcome.NotFound(Id);
        }
    }

    private static bool MatchesPlaceName(string requestedName, GeocodingContext context, PlaceResult candidate)
    {
        var candidateName = candidate.DisplayName?.Text;
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        var normalizedRequested = NormalizeName(requestedName);
        if (string.Equals(normalizedRequested, NormalizeName(candidateName), StringComparison.Ordinal))
        {
            return true;
        }

        var contextTokens = NormalizeName($"{context.City} {context.Country}")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        var identityTokens = normalizedRequested.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !contextTokens.Contains(token) && !GenericPlaceTokens.Contains(token))
            .ToArray();
        var candidateTokens = NormalizeName($"{candidateName} {candidate.FormattedAddress}")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        var matchedTokens = identityTokens.Count(candidateTokens.Contains);
        return identityTokens.Length > 0
            && matchedTokens == identityTokens.Length
            || identityTokens.Length >= 3
            && matchedTokens >= 2
            && matchedTokens / (double)identityTokens.Length >= 0.65;
    }

    internal static string NormalizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsValid(GeocodingLocation? location) => location is not null
        && IsValid(location.Latitude, location.Longitude);

    private static bool IsValid(PlaceLocation? location) => location is not null
        && IsValid(location.Latitude, location.Longitude);

    private static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && latitude is >= -90 and <= 90
        && double.IsFinite(longitude) && longitude is >= -180 and <= 180;

    private sealed record GeocodingResponse(string? Status, IReadOnlyList<GeocodingResult>? Results);
    private sealed record GeocodingResult(string? FormattedAddress, GeocodingGeometry? Geometry);
    private sealed record GeocodingGeometry(GeocodingLocation? Location);
    private sealed record GeocodingLocation(double Latitude, double Longitude);
    private sealed record PlacesResponse(IReadOnlyList<PlaceResult>? Places);
    private sealed record PlaceResult(PlaceDisplayName? DisplayName, string? FormattedAddress, PlaceLocation? Location);
    private sealed record PlaceDisplayName(string? Text);
    private sealed record PlaceLocation(double Latitude, double Longitude);
}
