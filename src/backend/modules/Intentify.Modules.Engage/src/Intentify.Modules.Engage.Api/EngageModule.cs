using Intentify.Modules.Auth.Api;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Infrastructure;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Leads.Application;
using Intentify.Shared.AI;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        var aiOptions = new AiOptions
        {
            ApiBaseUrl = configuration["Intentify:AI:ApiBaseUrl"],
            ApiKey = configuration["Intentify:AI:ApiKey"],
            ChatModel = configuration["Intentify:AI:ChatModel"],
            EmbeddingModel = configuration["Intentify:AI:EmbeddingModel"],
            TimeoutSeconds = configuration.GetValue<int?>("Intentify:AI:TimeoutSeconds") ?? 30,
            MaxPromptChars = configuration.GetValue<int?>("Intentify:AI:MaxPromptChars") ?? 0
        };
        services.TryAddSingleton(aiOptions);
        services.AddHttpClient(HttpChatCompletionClient.ClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<AiOptions>();
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);

            if (Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var apiBaseUri))
            {
                client.BaseAddress = apiBaseUri;
            }
        });
        services.TryAddSingleton<IChatCompletionClient>(serviceProvider =>
{
            var options = serviceProvider.GetRequiredService<AiOptions>();

            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl) && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(HttpChatCompletionClient.ClientName);
                return new HttpChatCompletionClient(options, httpClient);
            }

            return new NullChatCompletionClient(options);
        });
        services.AddSingleton<WidgetBootstrapHandler>();
        services.AddSingleton(serviceProvider =>
        {
            var sessionTimeoutMinutes = configuration.GetValue<int?>("Intentify:Engage:SessionTimeoutMinutes") ?? 30;
            return new ChatSendHandler(
                serviceProvider.GetRequiredService<ISiteRepository>(),
                serviceProvider.GetRequiredService<IEngageChatSessionRepository>(),
                serviceProvider.GetRequiredService<IEngageBotRepository>(),
                serviceProvider.GetRequiredService<IEngageChatMessageRepository>(),
                serviceProvider.GetRequiredService<IEngageHandoffTicketRepository>(),
                serviceProvider.GetRequiredService<CreateTicketHandler>(),
                serviceProvider.GetRequiredService<ILeadVisitorLinker>(),
                serviceProvider.GetRequiredService<UpsertLeadFromPromoEntryHandler>(),
                serviceProvider.GetRequiredService<RetrieveTopChunksHandler>(),
                serviceProvider.GetRequiredService<IChatCompletionClient>(),
                sessionTimeoutMinutes,
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ChatSendHandler>>());

        });
        services.AddSingleton<GetEngageBotHandler>();
        services.AddSingleton<UpdateEngageBotHandler>();
        services.AddSingleton<ListConversationsHandler>();
        services.AddSingleton<GetConversationMessagesHandler>();
        services.AddSingleton(serviceProvider =>
        {
            var sessionTimeoutMinutes = configuration.GetValue<int?>("Intentify:Engage:SessionTimeoutMinutes") ?? 30;
            return new GetWidgetConversationMessagesHandler(
                serviceProvider.GetRequiredService<ISiteRepository>(),
                serviceProvider.GetRequiredService<IEngageBotRepository>(),
                serviceProvider.GetRequiredService<IEngageChatSessionRepository>(),
                serviceProvider.GetRequiredService<IEngageChatMessageRepository>(),
                sessionTimeoutMinutes);
        });
        services.AddSingleton<VisitorContextBundleHandler>();
        services.AddSingleton<AiDecisionGenerationService>();
        services.AddSingleton<UpsertLeadFromPromoEntryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Public endpoints (for client websites)
        var publicGroup = endpoints.MapGroup("/engage");

        publicGroup.MapGet("/widget.js", EngageEndpoints.WidgetScriptAsync);
        publicGroup.MapGet("/widget/bootstrap", EngageEndpoints.WidgetBootstrapAsync);
        publicGroup.MapPost("/chat/send", EngageEndpoints.ChatSendAsync);
        publicGroup.MapGet("/widget/conversations/{sessionId}/messages", EngageEndpoints.GetWidgetConversationMessagesAsync);

        // Admin endpoints (dashboard / authenticated)
        var adminGroup = endpoints.MapGroup("/engage")
            .RequireAuthorization();

        adminGroup.MapGet("/bot", EngageEndpoints.GetBotAsync);
        adminGroup.MapPut("/bot", EngageEndpoints.UpdateBotAsync);
        adminGroup.MapGet("/conversations", EngageEndpoints.ListConversationsAsync);
        adminGroup.MapGet("/conversations/{sessionId}/messages", EngageEndpoints.GetConversationMessagesAsync);
    }
}
