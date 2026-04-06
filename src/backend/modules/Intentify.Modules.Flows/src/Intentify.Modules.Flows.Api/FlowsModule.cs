using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Application;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Infrastructure;
using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        services.AddHttpClient();

        // ── Email (Resend) ─────────────────────────────────────────────────────
        // Registered here so both Flows actions (SendEmail) and the Engage
        // DigestSchedulerService can resolve ResendEmailService from DI.
        var resendOptions = new ResendEmailOptions();
        configuration.GetSection(ResendEmailOptions.ConfigurationSection).Bind(resendOptions);
        services.AddSingleton(resendOptions);
        services.AddSingleton<ResendEmailService>();
        // ──────────────────────────────────────────────────────────────────────

        services.AddSingleton<IFlowsRepository, FlowsRepository>();
        services.AddSingleton<IFlowRunsRepository, FlowRunsRepository>();

        services.AddSingleton<CreateFlowService>();
        services.AddSingleton<UpdateFlowService>();
        services.AddSingleton<SetFlowEnabledService>();
        services.AddSingleton<GetFlowService>();
        services.AddSingleton<ListFlowsService>();
        services.AddSingleton<ListFlowRunsService>();
        services.AddSingleton<ExecuteFlowsForTriggerService>();
        services.AddSingleton<ICollectorEventObserver, CollectorPageViewFlowObserver>();
        services.AddSingleton<ICollectorEventObserver, ExitIntentFlowObserver>();
        services.AddSingleton<IIntelligenceObserver, IntelligenceFlowObserver>();
        services.AddSingleton<ILeadEventObserver, EngageLeadCapturedFlowObserver>();
        services.AddSingleton<ITicketEventObserver, EngageTicketCreatedFlowObserver>();
        services.AddSingleton<IEngageConversationObserver, EngageConversationCompletedFlowObserver>();
        services.AddSingleton<IVisitorEventObserver, VisitorReturnFlowObserver>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/flows")
            .AddEndpointFilter<RequireAuthFilter>();

        group.MapGet("/templates", FlowsEndpoints.GetTemplatesAsync);
        group.MapPost(string.Empty, FlowsEndpoints.CreateAsync);
        group.MapPut("/{id}", FlowsEndpoints.UpdateAsync);
        group.MapPost("/{id}/enable", FlowsEndpoints.EnableAsync);
        group.MapPost("/{id}/disable", FlowsEndpoints.DisableAsync);
        group.MapGet(string.Empty, FlowsEndpoints.ListAsync);
        group.MapGet("/{id}", FlowsEndpoints.GetAsync);
        group.MapGet("/{id}/runs", FlowsEndpoints.ListRunsAsync);
    }
}
