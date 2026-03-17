using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Infrastructure;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Sites.Api;

public sealed class SitesModule : IAppModule
{
    public string Name => "Sites";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IKeyGenerator, KeyGenerator>();
        services.AddSingleton<ISiteRepository, SiteRepository>();
        services.AddSingleton<ISiteKnowledgeCleanup, SiteKnowledgeCleanup>();
        services.AddSingleton<CreateSiteHandler>();
        services.AddSingleton<ListSitesHandler>();
        services.AddSingleton<UpdateAllowedOriginsHandler>();
        services.AddSingleton<UpdateSiteProfileHandler>();
        services.AddSingleton<DeleteSiteHandler>();
        services.AddSingleton<RotateKeysHandler>();
        services.AddSingleton<GetSiteKeysHandler>();
        services.AddSingleton<GetInstallationStatusHandler>();
        services.AddSingleton<GetPublicInstallationStatusHandler>();
        services.AddSingleton<GetInstallationDiagnosticsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/sites");
        var protectedGroup = group.MapGroup(string.Empty)
            .AddEndpointFilter<RequireAuthFilter>();

        protectedGroup.MapPost(string.Empty, SitesEndpoints.CreateSiteAsync);
        protectedGroup.MapGet(string.Empty, SitesEndpoints.ListSitesAsync);
        protectedGroup.MapPut("/{siteId}/profile", SitesEndpoints.UpdateSiteProfileAsync);
        protectedGroup.MapDelete("/{siteId}", SitesEndpoints.DeleteSiteAsync);
        protectedGroup.MapPut("/{siteId}/origins", SitesEndpoints.UpdateAllowedOriginsAsync);
        protectedGroup.MapPost("/{siteId}/keys/regenerate", SitesEndpoints.RegenerateKeysAsync);
        protectedGroup.MapGet("/{siteId}/keys", SitesEndpoints.GetSiteKeysAsync);
        protectedGroup.MapGet("/{siteId}/installation-status", SitesEndpoints.GetInstallationStatusAsync);
        protectedGroup.MapGet("/{siteId}/installation-diagnostics", SitesEndpoints.GetInstallationDiagnosticsAsync);

        group.MapGet("/installation/status", SitesEndpoints.GetPublicInstallationStatusAsync);
    }
}
