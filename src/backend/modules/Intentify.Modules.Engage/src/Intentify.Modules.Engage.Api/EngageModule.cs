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

        // Infrastructure repositories
        services.AddSingleton<IEngageChatSessionRepository, EngageChatSessionRepository>();
        services.AddSingleton<IEngageBotRepository, EngageBotRepository>();
        services.AddSingleton<IEngageChatMessageRepository, EngageChatMessageRepository>();
        services.AddSingleton<IEngageHandoffTicketRepository, EngageHandoffTicketRepository>();

        // AI configuration
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

        // NEW Engage Orchestrator Stack
        services.AddScoped<EngageConversationPolicy>();
        services.AddScoped<EngageInputInterpreter>();
        services.AddScoped<ResponseShaper>();
        services.AddScoped<EngageContextAnalyzer>();
        services.AddScoped<EngageStateRouter>();

        // Register all concrete state handlers
        services.AddScoped<IEngageState, GreetingState>();
        services.AddScoped<IEngageState, DiscoverState>();
        services.AddScoped<IEngageState, CaptureLeadState>();
        services.AddScoped<IEngageState, CaptureSupportState>();
        services.AddScoped<IEngageState, InformState>();
        services.AddScoped<IEngageState, ClarifyState>();
        services.AddScoped<IEngageState, ConfirmHandoffState>();

        services.AddScoped<EngageOrchestrator>();

        // Thin handler (the only one exposed to endpoints)
        services.AddScoped<ChatSendHandler>();

        // Existing handlers and services you still need
        services.AddSingleton<WidgetBootstrapHandler>();
        services.AddSingleton<GetEngageBotHandler>();
        services.AddSingleton<UpdateEngageBotHandler>();
        services.AddSingleton<ListConversationsHandler>();
        services.AddSingleton<GetOpportunityAnalyticsHandler>();
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
        services.AddSingleton<TenantVocabularyResolver>();
        services.AddSingleton<AiDecisionGenerationService>();
        services.AddSingleton<UpsertLeadFromPromoEntryHandler>();
        services.AddSingleton<CreateTicketHandler>(); // assuming this exists in Tickets.Application
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
        adminGroup.MapGet("/opportunities/analytics", EngageEndpoints.GetOpportunityAnalyticsAsync);
        adminGroup.MapGet("/conversations/{sessionId}/messages", EngageEndpoints.GetConversationMessagesAsync);
    }
}
