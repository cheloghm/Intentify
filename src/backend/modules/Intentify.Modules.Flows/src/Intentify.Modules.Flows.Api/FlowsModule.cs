using Intentify.Modules.Auth.Api;
using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Infrastructure;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Flows.Api;

public sealed class FlowsModule : IAppModule
{
    public string Name => "Flows";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IFlowsRepository, FlowsRepository>();
        services.AddSingleton<IFlowRunsRepository, FlowRunsRepository>();

        services.AddSingleton<CreateFlowService>();
        services.AddSingleton<UpdateFlowService>();
        services.AddSingleton<SetFlowEnabledService>();
        services.AddSingleton<GetFlowService>();
        services.AddSingleton<ListFlowsService>();
        services.AddSingleton<ListFlowRunsService>();
        services.AddSingleton<ExecuteFlowsForTriggerService>();
        services.AddSingleton<IIntelligenceObserver, IntelligenceFlowObserver>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/flows")
            .AddEndpointFilter<RequireAuthFilter>();

        group.MapPost(string.Empty, FlowsEndpoints.CreateAsync);
        group.MapPut("/{id}", FlowsEndpoints.UpdateAsync);
        group.MapPost("/{id}/enable", FlowsEndpoints.EnableAsync);
        group.MapPost("/{id}/disable", FlowsEndpoints.DisableAsync);
        group.MapGet(string.Empty, FlowsEndpoints.ListAsync);
        group.MapGet("/{id}", FlowsEndpoints.GetAsync);
        group.MapGet("/{id}/runs", FlowsEndpoints.ListRunsAsync);
    }
}
