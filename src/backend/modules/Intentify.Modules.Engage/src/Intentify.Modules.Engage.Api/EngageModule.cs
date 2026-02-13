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
        services.AddSingleton<IEngageChatMessageRepository, EngageChatMessageRepository>();
        services.AddSingleton<IEngageHandoffTicketRepository, EngageHandoffTicketRepository>();
        services.AddSingleton<WidgetBootstrapHandler>();
        services.AddSingleton<ChatSendHandler>();
        services.AddSingleton<ListConversationsHandler>();
        services.AddSingleton<GetConversationMessagesHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/engage");

        group.MapGet("/widget/bootstrap", EngageEndpoints.WidgetBootstrapAsync);
        group.MapPost("/chat/send", EngageEndpoints.ChatSendAsync);

        var protectedGroup = group.MapGroup(string.Empty)
            .AddEndpointFilter<RequireAuthFilter>();

        protectedGroup.MapGet("/conversations", EngageEndpoints.ListConversationsAsync);
        protectedGroup.MapGet("/conversations/{sessionId}/messages", EngageEndpoints.GetConversationMessagesAsync);
    }
}
