using Intentify.Modules.Ads.Api;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Api;
using Intentify.Modules.Engage.Api;
using Intentify.Modules.Flows.Api;
using Intentify.Modules.Intelligence.Api;
using Intentify.Modules.Knowledge.Api;
using Intentify.Modules.Leads.Api;
using Intentify.Modules.PlatformAdmin.Api;
using Intentify.Modules.Promos.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Modules.Tickets.Api;
using Intentify.Modules.Visitors.Api;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.AppHost;

public static class AppModuleCatalog
{
    public static IReadOnlyList<IAppModule> Modules { get; } =
    [
        new AuthModule(),
        new AdsModule(),
        new CollectorModule(),
        new SitesModule(),
        new KnowledgeModule(),
        new IntelligenceModule(),
        new PromosModule(),
        new LeadsModule(),
        new EngageModule(),
        new FlowsModule(),
        new VisitorsModule(),
        new TicketsModule(),
        new PlatformAdminModule()
    ];
}

internal sealed class AppModuleRegistry
{
    private readonly List<IAppModule> _modules = [];

    public IReadOnlyList<IAppModule> Modules => _modules;

    public void Register(IAppModule module)
    {
        _modules.Add(module);
    }
}

internal static class AppModuleExtensions
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var registry = new AppModuleRegistry();

        foreach (var module in AppModuleCatalog.Modules)
        {
            registry.Register(module);
            module.RegisterServices(services, configuration);
        }

        services.AddSingleton(registry);

        return services;
    }

    public static IEndpointRouteBuilder MapAppModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry = endpoints.ServiceProvider.GetRequiredService<AppModuleRegistry>();
        foreach (var module in registry.Modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
