using Intentify.Modules.Engage.Domain;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Application;

/// <summary>
/// Assembles the full turn context and invokes the AI decision service.
/// No signal-flag logic lives here — the AI reads intent directly from the conversation.
/// </summary>
public sealed class EngageContextAnalyzer
{
    private readonly AiDecisionGenerationService _aiDecisionService;
    private readonly ILogger<EngageContextAnalyzer> _logger;

    public EngageContextAnalyzer(AiDecisionGenerationService aiDecisionService, ILogger<EngageContextAnalyzer> logger)
    {
        _aiDecisionService = aiDecisionService;
        _logger = logger;
    }

    public async Task<EngageConversationContext> AnalyzeAsync(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        IReadOnlyCollection<string> tenantVocabulary,
        EngageBot bot,
        VisitorContextBundle? visitorBundle,
        CancellationToken ct)
    {
        // Use the last 15 messages as history context
        var historyWindow = recentMessages
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(15)
            .OrderBy(m => m.CreatedAtUtc)
            .ToArray();

        var lastAssistantQuestion = historyWindow
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .LastOrDefault();

        var isInitialTurn = historyWindow.All(m =>
            !string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));

        var sessionMemory = EngageSessionMemorySnapshot.FromSession(session, lastAssistantQuestion);

        EngageTurnDecision turnDecision;
        if (visitorBundle is not null)
        {
            turnDecision = await _aiDecisionService.GenerateAsync(
                visitorBundle,
                bot,
                tenantVocabulary,
                sessionMemory,
                historyWindow,
                ct);
        }
        else
        {
            // No visitor bundle — defer to a safe pass-through; state handler provides a minimal safe reply
            turnDecision = new EngageTurnDecision(
                Reply: string.Empty,
                Intent: "unknown — visitor bundle unavailable",
                CapturedSlots: new EngageTurnSlots(),
                CreateLead: false,
                CreateTicket: false,
                TicketSubject: null,
                TicketType: null,
                TicketSummary: null,
                SuggestedFollowUp: null,
                ConversationComplete: false,
                Confidence: 0.4m,
                IsValid: false,
                FallbackReason: "NoVisitorBundle");
        }

        _logger.LogInformation(
            "Engage turn analyzed. stage={Stage}; initial={Initial}; confidence={Confidence}; valid={Valid}; fallback={Fallback}; lead={Lead}; ticket={Ticket}; complete={Complete}",
            session.ConversationState ?? "Discover",
            isInitialTurn,
            turnDecision.Confidence,
            turnDecision.IsValid,
            turnDecision.FallbackReason ?? "none",
            turnDecision.CreateLead,
            turnDecision.CreateTicket,
            turnDecision.ConversationComplete);

        var analysis = new EngageAnalysisSummary(isInitialTurn, turnDecision.Confidence);
        return new EngageConversationContext(session, historyWindow, userMessage, turnDecision, lastAssistantQuestion, analysis);
    }
}

public sealed class EngageConversationContext
{
    public EngageChatSession Session { get; }
    public IReadOnlyCollection<EngageChatMessage> RecentMessages { get; }
    public string UserMessage { get; }
    public EngageTurnDecision TurnDecision { get; }
    public string? LastAssistantQuestion { get; }
    public EngageAnalysisSummary Analysis { get; }
    public EngageNextActionDecision? PrimaryActionDecision { get; private set; }

    public EngageConversationContext(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        EngageTurnDecision turnDecision,
        string? lastAssistantQuestion,
        EngageAnalysisSummary analysis)
    {
        Session = session;
        RecentMessages = recentMessages;
        UserMessage = userMessage;
        TurnDecision = turnDecision;
        LastAssistantQuestion = lastAssistantQuestion;
        Analysis = analysis;
    }

    public void SetPrimaryAction(EngageNextActionDecision decision) => PrimaryActionDecision = decision;
}

/// <summary>
/// Minimal context summary. Signal-flag logic has been removed — the AI reads intent directly.
/// </summary>
public sealed record EngageAnalysisSummary(bool IsInitialTurn, decimal AiConfidence);
