using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;
using RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

namespace RPAOtelRezervasyon.Infrastructure.Providers.HealthChecks;

/// <summary>
/// Sağlayıcı erişilebilirliğini raporlayan sağlık kontrolleri (docs/architecture.md). Sağlayıcı
/// erişilemezse servis yine çalışır (önbellek/fallback); bu yüzden sonuç <c>Degraded</c>'dir,
/// <c>Unhealthy</c> değil.
/// </summary>
public static class ProviderHealthChecks
{
    public const string ProviderTag = "provider";

    public static IServiceCollection AddProviderHealthChecks(this IServiceCollection services)
        => services.AddProviderHealthChecks("Geoapify");

    public static IServiceCollection AddProviderHealthChecks(this IServiceCollection services, string provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (string.Equals(provider, "Geoapify", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient("geoapify-health", client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddHealthChecks().AddCheck<GeoapifyHealthCheck>("geoapify", tags: [ProviderTag]);
        }
        else
        {
            services.AddHttpClient("google-maps-health", client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddHealthChecks().AddCheck<GoogleMapsHealthCheck>("google-maps", tags: [ProviderTag]);
        }

        return services;
    }
}

public sealed class GoogleMapsHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var endpoint = new Uri(options.Value.GeocodingBaseUrl, UriKind.Absolute);
            using var response = await httpClientFactory.CreateClient("google-maps-health")
                .GetAsync(endpoint.GetLeftPart(UriPartial.Authority), cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Google Maps API host yanıt veriyor.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Degraded("Google Maps API host erişilemiyor.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Google Maps health check zaman aşımına uğradı.");
        }
    }
}

/// <summary>Geoapify API host erişilebilirliğini anahtarsız ve kredi tüketmeden kontrol eder.</summary>
public sealed class GeoapifyHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<GeoapifyOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            using var response = await httpClientFactory.CreateClient("geoapify-health")
                .GetAsync(options.Value.BaseUrl, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Geoapify API host yanıt veriyor.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Degraded("Geoapify API host erişilemiyor.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Geoapify health check zaman aşımına uğradı.");
        }
    }
}

