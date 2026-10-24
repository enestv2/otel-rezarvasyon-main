using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public sealed class GoogleMapsFallbackStaticMapProvider(
    GoogleMapsStaticMapProvider primary,
    SchematicStaticMapProvider fallback) : IStaticMapProvider
{
    public string ProviderId => primary.ProviderId;

    public async Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        var outcome = await primary.RenderAsync(request, cancellationToken).ConfigureAwait(false);
        if (outcome.Status == OutcomeStatus.Found)
        {
            return outcome;
        }

        var fallbackOutcome = await fallback.RenderAsync(request, cancellationToken).ConfigureAwait(false);
        return fallbackOutcome.Status == OutcomeStatus.Found && outcome.Attribution is not null
            ? StaticMapOutcome.Found(
                fallbackOutcome.ProviderId,
                fallbackOutcome.Image!.Value,
                fallbackOutcome.ContentType!,
                outcome.Attribution,
                fallbackOutcome.IncludesDetailView)
            : fallbackOutcome;
    }
}
