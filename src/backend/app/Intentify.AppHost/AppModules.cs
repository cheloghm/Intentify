using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Api;
using Intentify.Modules.Sites.Api;
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
        new Intentify.Modules.Auth.Api.AuthModule(),
        new Intentify.Modules.Collector.Api.CollectorModule(),
        new Intentify.Modules.Sites.Api.SitesModule(),
        new Intentify.Modules.Visitors.Api.VisitorsModule()
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
