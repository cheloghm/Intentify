using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.AI;
using Microsoft.Extensions.Logging;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageOrchestrator
{
    private readonly EngageContextAnalyzer _contextAnalyzer;
    private readonly EngageStateRouter _stateRouter;
    private readonly ISiteRepository _siteRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly RetrieveTopChunksHandler _retrieveTopChunksHandler;
    private readonly VisitorContextBundleHandler _visitorContextBundleHandler;
    private readonly TenantVocabularyResolver _tenantVocabularyResolver;
    private readonly ILogger<EngageOrchestrator> _logger;

    public EngageOrchestrator(
        EngageContextAnalyzer contextAnalyzer,
        EngageStateRouter stateRouter,
        ISiteRepository siteRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageChatMessageRepository messageRepository,
        IEngageBotRepository botRepository,
        RetrieveTopChunksHandler retrieveTopChunksHandler,
        VisitorContextBundleHandler visitorContextBundleHandler,
        TenantVocabularyResolver tenantVocabularyResolver,
        ILogger<EngageOrchestrator> logger)
    {
        _contextAnalyzer = contextAnalyzer;
        _stateRouter = stateRouter;
        _siteRepository = siteRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _botRepository = botRepository;
        _retrieveTopChunksHandler = retrieveTopChunksHandler;
        _visitorContextBundleHandler = visitorContextBundleHandler;
        _tenantVocabularyResolver = tenantVocabularyResolver;
        _logger = logger;
    }

    public async Task<OperationResult<ChatSendResult>> HandleAsync(ChatSendCommand command, CancellationToken cancellationToken)
    {
        var site = await _siteRepository.GetByWidgetKeyAsync(command.WidgetKey, cancellationToken);
        if (site is null) return OperationResult<ChatSendResult>.NotFound();

        var bot = await _botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, cancellationToken);
        var session = await ResolveOrCreateSessionAsync(site, bot, command, cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = command.Message,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        var recentMessages = await _messageRepository.ListBySessionAsync(session.Id, cancellationToken);
        var knowledgeSummary = await GetKnowledgeSummaryAsync(site, bot, command.Message, cancellationToken);
        var tenantVocab = await _tenantVocabularyResolver.ResolveAsync(site.TenantId, site.Id, bot.BotId, cancellationToken);

        // FIXED: Correct constructor call using positional arguments + named where safe
        var visitorBundleResult = await _visitorContextBundleHandler.HandleAsync(
            new BuildVisitorContextBundleQuery(
                site.TenantId,           // TenantId
                site.Id,                 // SiteId
                null,                    // VisitorId (null)
                session.Id,              // Engage Session ID (4th parameter)
                command.Message,         // NormalizedUserMessage (5th parameter - this was the problem)
                3,                       // KnowledgeTop
                5,                       // TimelineLimit
                12,                      // EngageMessageLimit
                5,                       // TicketsLimit
                3),                      // PromoEntriesLimit
            cancellationToken);

        var visitorBundle = visitorBundleResult?.Value;

        var context = await _contextAnalyzer.AnalyzeAsync(
            session, 
            recentMessages, 
            command.Message, 
            knowledgeSummary, 
            tenantVocab, 
            bot, 
            visitorBundle, 
            cancellationToken);

        var result = await _stateRouter.RouteAndHandleAsync(context, cancellationToken);

        await _sessionRepository.UpdateStateAsync(session, cancellationToken);
        return result;
    }

    private async Task<EngageChatSession> ResolveOrCreateSessionAsync(
        Site site, EngageBot bot, ChatSendCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (command.SessionId.HasValue)
        {
            var existing = await _sessionRepository.GetByIdAsync(site.TenantId, site.Id, command.SessionId.Value, ct);
            if (existing != null && (now - existing.UpdatedAtUtc) <= TimeSpan.FromMinutes(30))
                return existing;
        }

        var newSession = new EngageChatSession
        {
            TenantId = site.TenantId,
            SiteId = site.Id,
            BotId = bot.BotId,
            WidgetKey = command.WidgetKey,
            CollectorSessionId = command.CollectorSessionId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConversationState = "Greeting"
        };

        await _sessionRepository.InsertAsync(newSession, ct);
        return newSession;
    }

    private async Task<string> GetKnowledgeSummaryAsync(Site site, EngageBot bot, string message, CancellationToken ct)
    {
        var chunks = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, message, 3, bot.BotId), ct);
        return string.Join("\n", chunks.Select(c => c.Content));
    }
}
