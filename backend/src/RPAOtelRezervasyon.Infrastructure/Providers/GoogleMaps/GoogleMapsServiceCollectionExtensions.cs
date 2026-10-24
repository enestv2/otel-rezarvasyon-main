using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Infrastructure.Providers.Common.Resilience;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;

public static class GoogleMapsServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleMapsProviders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => IsHttpsUrl(options.GeocodingBaseUrl)
                && IsHttpsUrl(options.PlacesBaseUrl)
                && IsHttpsUrl(options.RoutesBaseUrl)
                && IsHttpsUrl(options.StaticMapBaseUrl),
                "Google Maps uç noktaları mutlak HTTPS adresleri olmalıdır.")
            .ValidateOnStart();

        AddRateLimiter<GoogleMapsGeocodingProvider>(services);
        AddRateLimiter<GoogleMapsRouteDistanceProvider>(services);
        AddRateLimiter<GoogleMapsAirportSearchProvider>(services);
        AddRateLimiter<GoogleMapsStaticMapProvider>(services);

        AddResilientClient<GoogleMapsGeocodingProvider>(services, "google-geocoding-resilience");
        AddResilientClient<GoogleMapsRouteDistanceProvider>(services, "google-routes-resilience");
        AddResilientClient<GoogleMapsAirportSearchProvider>(services, "google-places-resilience");

        services.AddTransient<IGeocodingProvider>(provider => provider.GetRequiredService<GoogleMapsGeocodingProvider>());
        services.AddTransient<IRouteDistanceProvider>(provider => provider.GetRequiredService<GoogleMapsRouteDistanceProvider>());
        services.AddTransient<IWalkingRouteDistanceProvider>(provider => provider.GetRequiredService<GoogleMapsRouteDistanceProvider>());
        services.AddTransient<IAirportSearchProvider>(provider => provider.GetRequiredService<GoogleMapsAirportSearchProvider>());
        services.AddLogging(logging =>
        {
            logging.AddFilter("System.Net.Http.HttpClient.GoogleMapsGeocodingProvider", LogLevel.None);
            logging.AddFilter("System.Net.Http.HttpClient.GoogleMapsRouteDistanceProvider", LogLevel.None);
            logging.AddFilter("System.Net.Http.HttpClient.GoogleMapsAirportSearchProvider", LogLevel.None);
            logging.AddFilter("System.Net.Http.HttpClient.GoogleMapsStaticMapProvider", LogLevel.None);
        });

        return services;
    }

    public static IServiceCollection AddGoogleMapsStaticMapWithFallback(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<GoogleMapsStaticMapProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler("google-static-map-resilience", (builder, context) =>
            {
                var googleOptions = context.ServiceProvider.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;
                var limiter = context.ServiceProvider.GetRequiredKeyedService<RateLimiter>(
                    typeof(GoogleMapsStaticMapProvider).Name);
                builder
                    .AddRateLimiter(ResiliencePolicies.CreateRateLimiterStrategy(limiter))
                    .AddTimeout(TimeSpan.FromSeconds(30))
                    .AddRetry(ResiliencePolicies.CreateRetry(googleOptions.RetryMaxAttempts))
                    .AddCircuitBreaker(ResiliencePolicies.CreateCircuitBreaker())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });

        services.AddSingleton<SchematicStaticMapProvider>();
        services.AddTransient<IStaticMapProvider>(provider => new GoogleMapsFallbackStaticMapProvider(
            provider.GetRequiredService<GoogleMapsStaticMapProvider>(),
            provider.GetRequiredService<SchematicStaticMapProvider>()));
        return services;
    }

    private static void AddRateLimiter<TProvider>(IServiceCollection services)
        where TProvider : class
    {
        services.AddKeyedSingleton<RateLimiter>(typeof(TProvider).Name, (provider, _) =>
            ResiliencePolicies.CreateProviderRateLimiter(
                provider.GetRequiredService<IOptions<GoogleMapsOptions>>().Value.RequestsPerSecond));
    }

    private static void AddResilientClient<TProvider>(IServiceCollection services, string pipelineName)
        where TProvider : class
    {
        services.AddHttpClient<TProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler(pipelineName, (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;
                var limiter = context.ServiceProvider.GetRequiredKeyedService<RateLimiter>(typeof(TProvider).Name);
                builder
                    .AddRateLimiter(ResiliencePolicies.CreateRateLimiterStrategy(limiter))
                    .AddTimeout(TimeSpan.FromSeconds(30))
                    .AddRetry(ResiliencePolicies.CreateRetry(options.RetryMaxAttempts))
                    .AddCircuitBreaker(ResiliencePolicies.CreateCircuitBreaker())
                    .AddTimeout(TimeSpan.FromSeconds(10));
            });
    }

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
