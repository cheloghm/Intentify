using Intentify.Modules.Auth.Api;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Application.States;
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
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Api;

public sealed class EngageModule : IAppModule
{
    public string Name => "Engage";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Repositories
        services.AddSingleton<IEngageChatSessionRepository, EngageChatSessionRepository>();
        services.AddSingleton<IEngageBotRepository, EngageBotRepository>();
        services.AddSingleton<IEngageChatMessageRepository, EngageChatMessageRepository>();
        services.AddSingleton<IEngageHandoffTicketRepository, EngageHandoffTicketRepository>();

        // AI Configuration
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

        services.AddHttpClient(HttpChatCompletionClient.ClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<AiOptions>();
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);

            if (Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;
        });

        services.AddSingleton<IChatCompletionClient>(sp =>
        {
            var options = sp.GetRequiredService<AiOptions>();
            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl) && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient(HttpChatCompletionClient.ClientName);
                var aiLogger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<HttpChatCompletionClient>();
                return new HttpChatCompletionClient(options, client, aiLogger);
            }

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Intentify.Modules.Engage");
            logger.LogWarning(
                "AI client is not configured. Set Intentify:AI:ApiBaseUrl and Intentify:AI:ApiKey. " +
                "All AI-dependent features (chat responses, knowledge grounding) will be unavailable.");
            return new NullChatCompletionClient(options);
        });

        // === NEW ORCHESTRATOR STACK ===
        services.AddScoped<EngageConversationPolicy>();
        services.AddScoped<EngageInputInterpreter>();
        services.AddScoped<ResponseShaper>();
        services.AddScoped<EngageContextAnalyzer>();
        services.AddScoped<EngageNextActionSelector>();
        services.AddScoped<EngageStateRouter>();
        services.AddScoped<EngageBusinessOutcomeExecutor>();

        // All state handlers
        services.AddScoped<IEngageState, GreetingState>();
        services.AddScoped<IEngageState, DiscoverState>();
        services.AddScoped<IEngageState, CaptureLeadState>();

        services.AddScoped<EngageOrchestrator>();

        // Thin handler used by endpoints
        services.AddScoped<ChatSendHandler>();

        // Keep all your existing handlers
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
        services.AddSingleton<CreateTicketHandler>();
        services.AddSingleton<ListTicketsHandler>();
        services.AddSingleton<UpdateTicketHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var publicGroup = endpoints.MapGroup("/engage");
        publicGroup.MapGet("/widget.js", EngageEndpoints.WidgetScriptAsync);
        publicGroup.MapGet("/widget/bootstrap", EngageEndpoints.WidgetBootstrapAsync);
        publicGroup.MapPost("/chat/send", EngageEndpoints.ChatSendAsync);
        publicGroup.MapGet("/widget/conversations/{sessionId}/messages", EngageEndpoints.GetWidgetConversationMessagesAsync);

        var adminGroup = endpoints.MapGroup("/engage").RequireAuthorization();
        adminGroup.MapGet("/bot", EngageEndpoints.GetBotAsync);
        adminGroup.MapPut("/bot", EngageEndpoints.UpdateBotAsync);
        adminGroup.MapGet("/conversations", EngageEndpoints.ListConversationsAsync);
        adminGroup.MapGet("/opportunities/analytics", EngageEndpoints.GetOpportunityAnalyticsAsync);
        adminGroup.MapGet("/conversations/{sessionId}/messages", EngageEndpoints.GetConversationMessagesAsync);
    }
}
