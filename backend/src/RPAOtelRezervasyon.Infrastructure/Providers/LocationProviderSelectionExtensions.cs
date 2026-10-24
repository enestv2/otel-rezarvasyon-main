using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;
using RPAOtelRezervasyon.Infrastructure.Providers.GoogleMaps;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.Infrastructure.Providers;

public static class LocationProviderSelectionExtensions
{
    public const string ConfigurationKey = "LocationProvider:Provider";

    public static string AddConfiguredLocationProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration[ConfigurationKey]?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = "Geoapify";
        }

        if (string.Equals(provider, "Geoapify", StringComparison.OrdinalIgnoreCase))
        {
            services.AddGeoapifyProviders(configuration);
            services.AddStaticMapWithFallback(configuration);
            return "Geoapify";
        }

        if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "GoogleMaps", StringComparison.OrdinalIgnoreCase))
        {
            services.AddGoogleMapsProviders(configuration);
            services.AddGoogleMapsStaticMapWithFallback();
            return "Google";
        }

        throw new InvalidOperationException(
            $"{ConfigurationKey} değeri 'Geoapify' veya 'Google' olmalıdır; alınan değer: '{provider}'.");
    }
}
