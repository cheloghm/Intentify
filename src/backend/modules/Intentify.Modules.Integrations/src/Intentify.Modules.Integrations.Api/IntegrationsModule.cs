using Intentify.Modules.Auth.Api;
using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Integrations.Infrastructure;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Integrations.Api;

public sealed class IntegrationsModule : IAppModule
{
    public string Name => "Integrations";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpClient();

        services.AddSingleton<IWebhookRepository, WebhookRepository>();
        services.AddSingleton<IWebhookDispatcher, WebhookDispatcherImpl>();

        // Observer registrations — hook into existing event pipelines
        services.AddSingleton<ILeadEventObserver, WebhookLeadObserver>();
        services.AddSingleton<IVisitorEventObserver, WebhookVisitorObserver>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/integrations")
            .AddEndpointFilter<RequireAuthFilter>();

        group.MapGet("/webhooks",         IntegrationsEndpoints.ListWebhooksAsync);
        group.MapPost("/webhooks",        IntegrationsEndpoints.CreateWebhookAsync);
        group.MapDelete("/webhooks/{id}", IntegrationsEndpoints.DeleteWebhookAsync);
        group.MapPost("/webhooks/{id}/test", IntegrationsEndpoints.TestWebhookAsync);
    }
}
