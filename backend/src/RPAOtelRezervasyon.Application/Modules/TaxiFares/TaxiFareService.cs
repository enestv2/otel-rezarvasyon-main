using System.Globalization;
using System.Text;
using System.Collections.Concurrent;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.TaxiFares;

public sealed class TaxiFareService(ITaxiFareProvider provider, TaxiFareOptions options)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ResolvedFare?>>> _fareCache = new(StringComparer.Ordinal);

    public async Task<TaxiFareEstimate?> EstimateRoundTripAsync(
        string city,
        string? country,
        string currency,
        RouteMetrics eventCenterRoute,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentNullException.ThrowIfNull(eventCenterRoute);

        var cacheKey = $"{city.Trim().ToUpperInvariant()}|{country?.Trim().ToUpperInvariant()}|{currency}";
        var selectedFare = await _fareCache.GetOrAdd(cacheKey, _ => new Lazy<Task<ResolvedFare?>>(
            () => ResolveFareAsync(city, country, currency, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication))
            .Value.ConfigureAwait(false);
        if (selectedFare is null)
        {
            return null;
        }

        var distanceMeters = eventCenterRoute.DistanceMeters;
        var oneWayFare = selectedFare.OpeningFare + selectedFare.FarePerKm * (distanceMeters / 1000m);
        var roundTripFare = decimal.Round(oneWayFare * 2m, 2, MidpointRounding.AwayFromZero);

        return new TaxiFareEstimate(new NightlyPrice(roundTripFare, currency), selectedFare.UsedDefault,
            distanceMeters, eventCenterRoute.Kind, selectedFare.OpeningFare, selectedFare.FarePerKm);
    }

    private async Task<ResolvedFare?> ResolveFareAsync(string city, string? country, string currency, CancellationToken cancellationToken)
    {
        TaxiFareQuote? quote = null;
        try
        {
            quote = await provider.GetFareAsync(city, country, currency, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Tarifeyi alamamak öneri üretimini durdurmaz; yapılandırılmış varsayılan kullanılır.
        }

        if (IsUsable(quote, city, currency))
        {
            return new ResolvedFare(quote!.OpeningFare, quote.FarePerKm, UsedDefault: false);
        }

        if (string.Equals(options.DefaultCurrency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedFare(options.DefaultOpeningFare, options.DefaultFarePerKm, UsedDefault: true);
        }

        return options.DefaultRates.TryGetValue(currency, out var defaultRate)
               && defaultRate.OpeningFare > 0
               && defaultRate.FarePerKm > 0
            ? new ResolvedFare(defaultRate.OpeningFare, defaultRate.FarePerKm, UsedDefault: true)
            : null;
    }

    private bool IsUsable(TaxiFareQuote? quote, string city, string currency)
    {
        if (quote is null
            || quote.OpeningFare <= 0
            || quote.FarePerKm <= 0
            || !string.Equals(quote.Currency, currency, StringComparison.Ordinal)
            || !string.Equals(Normalize(quote.City), Normalize(city), StringComparison.Ordinal)
            || quote.EffectiveDate > DateOnly.FromDateTime(DateTime.UtcNow)
            || DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - quote.EffectiveDate.DayNumber > options.MaxFareAgeDays
            || !Uri.TryCreate(quote.SourceUrl, UriKind.Absolute, out var source)
            || source.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return true;
    }

    private static string Normalize(string value) => string.Join(' ', value
        .Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant));

    private sealed record ResolvedFare(decimal OpeningFare, decimal FarePerKm, bool UsedDefault);
}
