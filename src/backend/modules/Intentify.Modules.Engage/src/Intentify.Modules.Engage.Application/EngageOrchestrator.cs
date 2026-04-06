using Intentify.Modules.Engage.Domain;
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
    private readonly EngageNextActionSelector _nextActionSelector;
    private readonly EngageStateRouter _stateRouter;
    private readonly ISiteRepository _siteRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly VisitorContextBundleHandler _visitorContextBundleHandler;
    private readonly TenantVocabularyResolver _tenantVocabularyResolver;
    private readonly EngageBusinessOutcomeExecutor _businessOutcomeExecutor;
    private readonly IReadOnlyCollection<IEngageConversationObserver> _conversationObservers;
    private readonly ILogger<EngageOrchestrator> _logger;

    public EngageOrchestrator(
        EngageContextAnalyzer contextAnalyzer,
        EngageNextActionSelector nextActionSelector,
        EngageStateRouter stateRouter,
        ISiteRepository siteRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageChatMessageRepository messageRepository,
        IEngageBotRepository botRepository,
        VisitorContextBundleHandler visitorContextBundleHandler,
        TenantVocabularyResolver tenantVocabularyResolver,
        EngageBusinessOutcomeExecutor businessOutcomeExecutor,
        IEnumerable<IEngageConversationObserver> conversationObservers,
        ILogger<EngageOrchestrator> logger)
    {
        _contextAnalyzer = contextAnalyzer;
        _nextActionSelector = nextActionSelector;
        _stateRouter = stateRouter;
        _siteRepository = siteRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _botRepository = botRepository;
        _visitorContextBundleHandler = visitorContextBundleHandler;
        _tenantVocabularyResolver = tenantVocabularyResolver;
        _businessOutcomeExecutor = businessOutcomeExecutor;
        _conversationObservers = conversationObservers.ToArray();
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
            tenantVocab,
            bot,
            visitorBundle,
            cancellationToken);

        context.SetPrimaryAction(_nextActionSelector.Select(context));
        _logger.LogInformation(
            "Engage action selected. action={Action} target={Target} reason={Reason} initial={Initial} confidence={Confidence}",
            context.PrimaryActionDecision?.Action,
            context.PrimaryActionDecision?.TargetState,
            context.PrimaryActionDecision?.Reason,
            context.Analysis.IsInitialTurn,
            context.Analysis.AiConfidence);

        var result = await _stateRouter.RouteAndHandleAsync(context, cancellationToken);

        if (result.Status == OperationStatus.Success && result.Value is not null)
        {
            var businessOutcome = await _businessOutcomeExecutor.ExecuteAsync(context, command, result.Value, cancellationToken);
            if (businessOutcome.TicketTouched)
            {
                result = OperationResult<ChatSendResult>.Success(result.Value with { TicketCreated = true });
            }

            await _messageRepository.InsertAsync(new EngageChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = result.Value.Response,
                CreatedAtUtc = DateTime.UtcNow,
                Confidence = result.Value.Confidence,
                Citations = result.Value.Sources.Select(item => new EngageCitation
                {
                    SourceId = item.SourceId,
                    ChunkId = item.ChunkId,
                    ChunkIndex = item.ChunkIndex
                }).ToArray()
            }, cancellationToken);

            _logger.LogInformation("Engage outcome persisted. completeAfter={CompleteAfter}; pendingMode={PendingMode}; ticketTouched={TicketTouched}; leadTouched={LeadTouched}",
                session.IsConversationComplete,
                session.PendingCaptureMode,
                businessOutcome.TicketTouched,
                businessOutcome.LeadTouched);
        }

        session.UpdatedAtUtc = DateTime.UtcNow;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);

        if (session.IsConversationComplete && _conversationObservers.Count > 0)
        {
            var completedNotification = new ConversationCompletedNotification(
                session.TenantId,
                session.SiteId,
                session.Id,
                session.UpdatedAtUtc);

            foreach (var observer in _conversationObservers)
            {
                await observer.OnConversationCompletedAsync(completedNotification, cancellationToken);
            }
        }

        if (result.Status == OperationStatus.Success && result.Value is not null)
        {
            var playbookResponse = ApplyTenantPlaybook(result.Value.Response, bot, tenantVocab);
            result = OperationResult<ChatSendResult>.Success(result.Value with { Response = playbookResponse });
        }

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
            ConversationState = "Greeting",
            IsConversationComplete = false,
            LastAssistantAskType = "none"
        };

        await _sessionRepository.InsertAsync(newSession, ct);
        return newSession;
    }

    internal static string ApplyTenantPlaybook(string response, EngageBot bot, IReadOnlyCollection<string> tenantVocabulary)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return response;
        }

        var tuned = response;

        if (string.Equals(bot.Tone, "formal", StringComparison.OrdinalIgnoreCase))
        {
            tuned = tuned.Replace("Hi!", "Hello.", StringComparison.Ordinal);
        }

        if (string.Equals(bot.Verbosity, "concise", StringComparison.OrdinalIgnoreCase))
        {
            var firstQuestion = tuned.IndexOf('?', StringComparison.Ordinal);
            if (firstQuestion > 0)
            {
                tuned = tuned[..(firstQuestion + 1)];
            }
        }

        if (!string.IsNullOrWhiteSpace(bot.FallbackStyle)
            && bot.FallbackStyle.Contains("tenant-vocab", StringComparison.OrdinalIgnoreCase)
            && tenantVocabulary.Count > 0
            && !tuned.Contains(tenantVocabulary.First(), StringComparison.OrdinalIgnoreCase))
        {
            tuned = $"{tuned} ({tenantVocabulary.First()})";
        }

        return tuned;
    }
}
