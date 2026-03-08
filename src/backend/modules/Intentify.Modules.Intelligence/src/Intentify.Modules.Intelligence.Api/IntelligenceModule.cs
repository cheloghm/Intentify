using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Intelligence.Api;

public sealed class IntelligenceModule : IAppModule
{
    public string Name => "Intelligence";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var googleSearchOptions = new GoogleSearchOptions();
        configuration.GetSection(GoogleSearchOptions.ConfigurationSection).Bind(googleSearchOptions);
        services.AddSingleton(googleSearchOptions);

        var googleTrendsOptions = new GoogleTrendsOptions();
        configuration.GetSection(GoogleTrendsOptions.ConfigurationSection).Bind(googleTrendsOptions);
        services.AddSingleton(googleTrendsOptions);

        var googleAdsOptions = new GoogleAdsOptions();
        configuration.GetSection(GoogleAdsOptions.ConfigurationSection).Bind(googleAdsOptions);
        services.AddSingleton(googleAdsOptions);

        var searchOptions = new IntelligenceSearchOptions();
        configuration.GetSection(IntelligenceSearchOptions.ConfigurationSection).Bind(searchOptions);
        services.AddSingleton(searchOptions);

        RegisterHttpClient(services, GoogleSearchProvider.ClientName, googleSearchOptions.BaseUrl, googleSearchOptions.TimeoutSeconds);
        RegisterHttpClient(services, IntelligenceHttpClientNames.GoogleTrends, googleTrendsOptions.BaseUrl, googleTrendsOptions.TimeoutSeconds);
        RegisterHttpClient(services, GoogleAdsHistoricalMetricsProvider.ClientName, googleAdsOptions.BaseUrl, googleAdsOptions.TimeoutSeconds);

        services.AddSingleton<IExternalSearchProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IntelligenceSearchOptions>();
            var providerName = options.Provider?.Trim() ?? "Google";

            var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            if (providerName.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var httpClient = clientFactory.CreateClient(GoogleSearchProvider.ClientName);
                var configuredSearchOptions = serviceProvider.GetRequiredService<GoogleSearchOptions>();
                return new GoogleSearchProvider(httpClient, configuredSearchOptions);
            }

            if (providerName.Equals("GoogleAds", StringComparison.OrdinalIgnoreCase))
            {
                var httpClient = clientFactory.CreateClient(GoogleAdsHistoricalMetricsProvider.ClientName);
                var configuredAdsOptions = serviceProvider.GetRequiredService<GoogleAdsOptions>();
                var profileRepository = serviceProvider.GetRequiredService<IIntelligenceProfileRepository>();
                return new GoogleAdsHistoricalMetricsProvider(httpClient, configuredAdsOptions, profileRepository);
            }

            throw new InvalidOperationException($"Unsupported intelligence search provider '{providerName}'.");
        });

        services.AddSingleton<IIntelligenceTrendsRepository, IntelligenceTrendsRepository>();
        services.AddSingleton<IIntelligenceProfileRepository, IntelligenceProfileRepository>();
        services.AddSingleton<RefreshIntelligenceTrendsService>();
        services.AddSingleton<QueryIntelligenceTrendsService>();
        services.AddSingleton<GetIntelligenceStatusService>();
        services.AddSingleton<UpsertIntelligenceProfileService>();
        services.AddSingleton<GetIntelligenceProfileService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/intelligence")
            .RequireAuthorization();

        group.MapPost("/refresh", IntelligenceEndpoints.RefreshAsync);
        group.MapGet("/trends", IntelligenceEndpoints.GetTrendsAsync);
        group.MapGet("/status", IntelligenceEndpoints.GetStatusAsync);
        group.MapGet("/dashboard", IntelligenceEndpoints.GetDashboardAsync);
        group.MapPut("/profiles/{siteId}", IntelligenceEndpoints.UpsertProfileAsync);
        group.MapGet("/profiles/{siteId}", IntelligenceEndpoints.GetProfileAsync);
    }

    private static void RegisterHttpClient(IServiceCollection services, string clientName, string? baseUrl, int timeoutSeconds)
    {
        services.AddHttpClient(clientName, (_, client) =>
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10);
        });
    }
}

internal static class IntelligenceHttpClientNames
{
    public const string GoogleTrends = "intelligence-google-trends";
}
