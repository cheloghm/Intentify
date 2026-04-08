using Intentify.Modules.Collector.Application;
using Intentify.Modules.Collector.Infrastructure;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Collector.Api;

public sealed class CollectorModule : IAppModule
{
    public string Name => "Collector";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddCors(options =>
        {
            options.AddPolicy("CollectorPolicy", policy =>
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader());
        });

        services.AddSingleton<ICollectorEventRepository, CollectorEventRepository>();
        services.AddSingleton<ISiteLookupRepository, SiteLookupRepository>();
        services.AddSingleton<IngestCollectorEventHandler>();
        services.AddSingleton<IVisitorConsentWriter, VisitorConsentWriter>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/collector")
            .RequireCors("CollectorPolicy");

        group.MapGet("/sdk.js", CollectorEndpoints.GetSdkAsync);
        group.MapGet("/sdk/bootstrap", CollectorEndpoints.GetSdkBootstrapAsync);
        group.MapGet("/tracker.js", CollectorEndpoints.GetTrackerAsync);
        group.MapPost("/events", CollectorEndpoints.CollectEventAsync);
        group.MapPost("/consent", GdprConsentEndpoint.HandleAsync).AllowAnonymous();
    }
}

