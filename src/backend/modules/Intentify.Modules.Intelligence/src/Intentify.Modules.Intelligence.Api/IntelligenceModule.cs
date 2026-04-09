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

        // ── SerpApi provider (primary, used for initial release) ──────────────
        var serpApiOptions = new SerpApiTrendsOptions();
        configuration.GetSection(SerpApiTrendsOptions.ConfigurationSection).Bind(serpApiOptions);
        services.AddSingleton(serpApiOptions);

        // ── Provider selection ────────────────────────────────────────────────
        var searchOptions = new IntelligenceSearchOptions();
        configuration.GetSection(IntelligenceSearchOptions.ConfigurationSection).Bind(searchOptions);
        services.AddSingleton(searchOptions);

        // ── Recurring refresh options ─────────────────────────────────────────
        var recurringRefreshOptions = new RecurringIntelligenceRefreshOptions();
        configuration.GetSection(RecurringIntelligenceRefreshOptions.ConfigurationSection).Bind(recurringRefreshOptions);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(recurringRefreshOptions));

        // ── HTTP client for SerpApi ───────────────────────────────────────────
        services.AddHttpClient(SerpApiTrendsProvider.ClientName, (_, client) =>
        {
            if (Uri.TryCreate(serpApiOptions.BaseUrl, UriKind.Absolute, out var baseUri))
                client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromSeconds(
                serpApiOptions.TimeoutSeconds > 0 ? serpApiOptions.TimeoutSeconds : 15);
        });

        // ── Provider factory ──────────────────────────────────────────────────
        // Currently always resolves to SerpApiTrendsProvider.
        // When new providers are added in future phases, extend this switch.
        services.AddSingleton<IExternalSearchProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opts    = sp.GetRequiredService<SerpApiTrendsOptions>();
            return new SerpApiTrendsProvider(
                factory.CreateClient(SerpApiTrendsProvider.ClientName),
                opts);
        });

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddSingleton<IIntelligenceTrendsRepository,  IntelligenceTrendsRepository>();
        services.AddSingleton<IIntelligenceProfileRepository, IntelligenceProfileRepository>();

        // ── Application services ──────────────────────────────────────────────
        services.AddSingleton<INetworkSignalsService, NetworkSignalsService>();
        services.AddSingleton<IIntelligenceObserver, NoOpIntelligenceObserver>();
        services.AddSingleton<RefreshIntelligenceTrendsService>();
        services.AddSingleton<QueryIntelligenceTrendsService>();
        services.AddSingleton<GetIntelligenceStatusService>();
        services.AddSingleton<GetSiteInsightsSummaryService>();
        services.AddSingleton<UpsertIntelligenceProfileService>();
        services.AddSingleton<GetIntelligenceProfileService>();

        // ── Background refresh worker ─────────────────────────────────────────
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRecurringIntelligenceRefreshExecutor, RecurringIntelligenceRefreshExecutor>();
        services.AddSingleton<RecurringIntelligenceRefreshOrchestrator>();
        services.AddHostedService<RecurringIntelligenceRefreshWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/intelligence").RequireAuthorization();

        group.MapPost("/refresh",          IntelligenceEndpoints.RefreshAsync);
        group.MapGet("/trends",            IntelligenceEndpoints.GetTrendsAsync);
        group.MapGet("/status",            IntelligenceEndpoints.GetStatusAsync);
        group.MapGet("/dashboard",         IntelligenceEndpoints.GetDashboardAsync);
        group.MapGet("/site-summary",      IntelligenceEndpoints.GetSiteSummaryAsync);
        group.MapPut("/profiles/{siteId}", IntelligenceEndpoints.UpsertProfileAsync);
        group.MapGet("/profiles/{siteId}", IntelligenceEndpoints.GetProfileAsync);
        group.MapGet("/network-signals",  IntelligenceEndpoints.GetNetworkSignalsAsync);
    }
}

internal sealed class NoOpIntelligenceObserver : IIntelligenceObserver
{
    public Task OnTrendsUpdated(IntelligenceTrendsUpdatedNotification notification, CancellationToken ct)
        => Task.CompletedTask;
}
