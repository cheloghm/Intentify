using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        var recurringRefreshOptions = new RecurringIntelligenceRefreshOptions();
        configuration.GetSection(RecurringIntelligenceRefreshOptions.ConfigurationSection).Bind(recurringRefreshOptions);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(recurringRefreshOptions));

        RegisterHttpClient(services, GoogleSearchProvider.ClientName, googleSearchOptions.BaseUrl, googleSearchOptions.TimeoutSeconds);
        RegisterHttpClient(services, GoogleTrendsProvider.ClientName, googleTrendsOptions.BaseUrl, googleTrendsOptions.TimeoutSeconds);
        RegisterHttpClient(services, GoogleAdsHistoricalMetricsProvider.ClientName, googleAdsOptions.BaseUrl, googleAdsOptions.TimeoutSeconds);

        services.AddSingleton<IExternalSearchProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IntelligenceSearchOptions>();
            var providerName = options.Provider?.Trim() ?? "Google";
            var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            return providerName.ToLowerInvariant() switch
            {
                "googletrends" or "trends" => new GoogleTrendsProvider(
                    clientFactory.CreateClient(GoogleTrendsProvider.ClientName),
                    serviceProvider.GetRequiredService<GoogleTrendsOptions>()),
                "googleads" or "ads" => new GoogleAdsHistoricalMetricsProvider(
                    clientFactory.CreateClient(GoogleAdsHistoricalMetricsProvider.ClientName),
                    serviceProvider.GetRequiredService<GoogleAdsOptions>(),
                    serviceProvider.GetRequiredService<IIntelligenceProfileRepository>()),
                _ => new GoogleSearchProvider(
                    clientFactory.CreateClient(GoogleSearchProvider.ClientName),
                    serviceProvider.GetRequiredService<GoogleSearchOptions>())
            };
        });

        services.AddSingleton<IIntelligenceTrendsRepository, IntelligenceTrendsRepository>();
        services.AddSingleton<IIntelligenceProfileRepository, IntelligenceProfileRepository>();
        services.AddSingleton<RefreshIntelligenceTrendsService>();
        services.AddSingleton<QueryIntelligenceTrendsService>();
        services.AddSingleton<GetIntelligenceStatusService>();
        services.AddSingleton<UpsertIntelligenceProfileService>();
        services.AddSingleton<GetIntelligenceProfileService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRecurringIntelligenceRefreshExecutor, RecurringIntelligenceRefreshExecutor>();
        services.AddSingleton<RecurringIntelligenceRefreshOrchestrator>();
        services.AddHostedService<RecurringIntelligenceRefreshWorker>();
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


