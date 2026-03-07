using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Visitors.Api;

public sealed class VisitorsModule : IAppModule
{
    public string Name => "Visitors";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new VisitorsRetentionOptions
        {
            RetentionDays = configuration.GetValue<int?>("Intentify:Visitors:RetentionDays")
        });

        services.AddSingleton<IVisitorRepository, VisitorRepository>();
        services.AddSingleton<IVisitorTimelineReader, VisitorTimelineReader>();
        services.AddSingleton<UpsertVisitorFromCollectorEventHandler>();
        services.AddSingleton<ICollectorEventObserver, CollectorVisitorEventObserver>();
        services.AddSingleton<ListVisitorsHandler>();
        services.AddSingleton<GetVisitorDetailHandler>();
        services.AddSingleton<GetVisitorTimelineHandler>();
        services.AddSingleton<GetVisitCountWindowsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/visitors")
            .AddEndpointFilter<RequireAuthFilter>();

        group.MapGet(string.Empty, VisitorsEndpoints.ListVisitorsAsync);
        group.MapGet("/{visitorId}", VisitorsEndpoints.GetVisitorAsync);
        group.MapGet("/{visitorId}/timeline", VisitorsEndpoints.GetTimelineAsync);
        group.MapGet("/visits/counts", VisitorsEndpoints.GetVisitCountsAsync);
    }
}
