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
        services.AddSingleton<GetPlatformDashboardHandler>();
        services.AddSingleton<FeedbackStore>();
        services.AddSingleton<FeatureInterestStore>();
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
        group.MapGet("/dashboard", PlatformAdminEndpoints.GetDashboardAsync);
        group.MapGet("/feedback", PlatformAdminEndpoints.ListFeedbackAsync);
        group.MapPatch("/feedback/{id}/status", PlatformAdminEndpoints.UpdateFeedbackStatusAsync);

        var feedbackGroup = endpoints.MapGroup("/feedback")
            .RequireAuthorization();
        feedbackGroup.MapPost("", PlatformAdminEndpoints.SubmitFeedbackAsync);

        var notifyGroup = endpoints.MapGroup("/notify")
            .RequireAuthorization();
        notifyGroup.MapPost("/feature", PlatformAdminEndpoints.RegisterFeatureInterestAsync);

        endpoints.MapGet("/notify/feature/admin", PlatformAdminEndpoints.ListFeatureInterestAsync)
            .RequireAuthorization(PolicyName);
    }
}
