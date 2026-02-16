using Intentify.Modules.Auth.Api;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Engage.Api;

public sealed class EngageModule : IAppModule
{
    public string Name => "Engage";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IEngageChatSessionRepository, EngageChatSessionRepository>();
        services.AddSingleton<IEngageBotRepository, EngageBotRepository>();
        services.AddSingleton<IEngageChatMessageRepository, EngageChatMessageRepository>();
        services.AddSingleton<IEngageHandoffTicketRepository, EngageHandoffTicketRepository>();
        services.AddSingleton<WidgetBootstrapHandler>();
        services.AddSingleton<ChatSendHandler>();
        services.AddSingleton<ListConversationsHandler>();
        services.AddSingleton<GetConversationMessagesHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Public endpoints (for client websites)
        var publicGroup = endpoints.MapGroup("/engage");

        publicGroup.MapGet("/widget.js", EngageEndpoints.WidgetScriptAsync);
        publicGroup.MapGet("/widget/bootstrap", EngageEndpoints.WidgetBootstrapAsync);
        publicGroup.MapPost("/chat/send", EngageEndpoints.ChatSendAsync);

        // Admin endpoints (dashboard / authenticated)
        var adminGroup = endpoints.MapGroup("/engage")
            .RequireAuthorization();

        adminGroup.MapGet("/conversations", EngageEndpoints.ListConversationsAsync);
        adminGroup.MapGet("/conversations/{sessionId}/messages", EngageEndpoints.GetConversationMessagesAsync);
    }
}
