using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Http;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

public sealed class GeoapifyGeocodingProvider(
    HttpClient httpClient,
    IOptions<GeoapifyOptions> options,
    ILogger<GeoapifyGeocodingProvider> logger) : IGeocodingProvider
{
    public const string Id = "geoapify";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> GenericVenueTokens = new(StringComparer.Ordinal)
    {
        "arena", "center", "centre", "conference", "congress", "convention", "expo", "fair", "hall",
        "hospital", "hotel", "merkez", "merkezi", "salon", "salonu", "stadium", "university", "venue",
        "fuar", "hastane", "hastanesi", "kongre", "oteli", "otel", "universitesi",
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

        var query = new List<string>
        {
            $"name={Uri.EscapeDataString(placeName.Trim())}",
            "format=json",
            "limit=5",
            $"apiKey={Uri.EscapeDataString(options.Value.ApiKey)}",
        };
        if (!string.IsNullOrWhiteSpace(context.City))
        {
            query.Add($"city={Uri.EscapeDataString(context.City.Trim())}");
        }
        if (!string.IsNullOrWhiteSpace(context.Country))
        {
            query.Add($"country={Uri.EscapeDataString(context.Country.Trim())}");
        }

        try
        {
            using var response = await httpClient.GetAsync(
                $"v1/geocode/search?{string.Join('&', query)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Geoapify geocoding yanıtı başarısız: {StatusCode}.", (int)response.StatusCode);
                return ProviderHttpStatus.IsTransient(response.StatusCode)
                    ? GeocodingOutcome.TransientError(Id)
                    : GeocodingOutcome.ProviderError(Id);
            }

            var payload = await DeserializeAsync(response, cancellationToken).ConfigureAwait(false);
            var result = payload?.Results?.FirstOrDefault(candidate =>
                IsValidCoordinate(candidate.Latitude, candidate.Longitude)
                && MatchesPlaceName(placeName, context, candidate));
            if (result is null || !IsValidCoordinate(result.Latitude, result.Longitude))
            {
                return GeocodingOutcome.NotFound(Id);
            }

            return GeocodingOutcome.Found(Id, new GeoPoint(result.Latitude, result.Longitude), result.Formatted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Geoapify geocoding zaman aşımına uğradı.");
            return GeocodingOutcome.TransientError(Id);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Geoapify geocoding isteği başarısız oldu.");
            return GeocodingOutcome.TransientError(Id);
        }
        catch (JsonException)
        {
            logger.LogWarning("Geoapify geocoding yanıtı beklenen şemaya uymuyor.");
            return GeocodingOutcome.TransientError(Id);
        }
    }

    private static async Task<GeoapifyGeocodeResponse?> DeserializeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<GeoapifyGeocodeResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsValidCoordinate(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude)
        && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static bool MatchesPlaceName(string requestedName, GeocodingContext context, GeoapifyGeocodeResult result)
    {
        var candidateName = result.Name;
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            candidateName = result.Formatted?.Split(',', 2)[0];
        }

        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        var normalizedRequested = NormalizeName(requestedName);
        var normalizedCandidate = NormalizeName(candidateName);
        if (string.Equals(normalizedRequested, normalizedCandidate, StringComparison.Ordinal))
        {
            return true;
        }

        var contextTokens = NormalizeName($"{context.City} {context.Country}")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        var requestedTokens = normalizedRequested.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requestedIdentityTokens = requestedTokens
            .Where(token => !contextTokens.Contains(token) && !GenericVenueTokens.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
        var candidateIdentityTokens = candidateTokens
            .Where(token => !contextTokens.Contains(token) && !GenericVenueTokens.Contains(token))
            .ToArray();

        return requestedIdentityTokens.Count > 0
            && candidateIdentityTokens.Length > 0
            && requestedIdentityTokens.All(candidateIdentityTokens.Contains)
            && candidateTokens.All(token => requestedTokens.Contains(token, StringComparer.Ordinal)
                || contextTokens.Contains(token)
                || GenericVenueTokens.Contains(token));
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            result.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return result.ToString();
    }
}
