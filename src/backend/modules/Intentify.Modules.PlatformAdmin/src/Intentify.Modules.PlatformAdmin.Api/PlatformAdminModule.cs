using Intentify.Modules.PlatformAdmin.Application;
using Intentify.Modules.PlatformAdmin.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.PlatformAdmin.Api;

public sealed class PlatformAdminModule : IAppModule
{
    public const string PolicyName = "PlatformAdmin";

    public string Name => "PlatformAdmin";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IPlatformAdminReadRepository, PlatformAdminReadRepository>();
        services.AddSingleton<GetPlatformSummaryHandler>();
        services.AddSingleton<ListPlatformTenantsHandler>();
        services.AddSingleton<GetPlatformTenantDetailHandler>();
        services.AddSingleton<GetPlatformOperationalSummaryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/platform-admin")
            .RequireAuthorization(PolicyName);

        group.MapGet("/summary", PlatformAdminEndpoints.GetSummaryAsync);
        group.MapGet("/tenants", PlatformAdminEndpoints.ListTenantsAsync);
        group.MapGet("/tenants/{tenantId}", PlatformAdminEndpoints.GetTenantDetailAsync);
        group.MapGet("/operations/summary", PlatformAdminEndpoints.GetOperationalSummaryAsync);
    }
}
