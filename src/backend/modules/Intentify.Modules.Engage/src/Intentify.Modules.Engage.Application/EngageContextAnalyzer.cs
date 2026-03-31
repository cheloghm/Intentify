using Intentify.Modules.Engage.Domain;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageContextAnalyzer
{
    private readonly AiDecisionGenerationService _aiDecisionService;
    private readonly EngageConversationPolicy _policy;
    private readonly ILogger<EngageContextAnalyzer> _logger;

    public EngageContextAnalyzer(
        AiDecisionGenerationService aiDecisionService,
        EngageConversationPolicy policy,
        ILogger<EngageContextAnalyzer> logger)
    {
        _aiDecisionService = aiDecisionService;
        _policy = policy;
        _logger = logger;
    }

    public async Task<EngageConversationContext> AnalyzeAsync(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        string knowledgeSummary,
        IReadOnlyCollection<string> tenantVocabulary,
        EngageBot bot,
        VisitorContextBundle? visitorBundle,
        CancellationToken ct)
    {
        var lastQuestion = recentMessages
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .LastOrDefault();

        var decision = visitorBundle != null
            ? await _aiDecisionService.GenerateAsync(visitorBundle, ct)
            : new AiDecisionContract(
                "stage7.v1",
                "fallback",
                null,
                0.4m,
                [],
                AiDecisionValidationStatus.Valid,
                [],
                null,
                true,
                "NoVisitorBundle",
                null);

        var analysis = BuildAnalysisSummary(session, recentMessages, userMessage, knowledgeSummary, decision);

        _logger.LogInformation(
            "Engage turn analyzed. stage={Stage}; complete={IsComplete}; close={IsClose}; support={IsSupport}; factual={IsFactual}; capture={IsCapture}; reopen={ShouldReopen}; answeredPrior={AnsweredPrior}; aiConfidence={AiConfidence}",
            session.ConversationState ?? "Discover",
            session.IsConversationComplete,
            analysis.IsCloseSignal,
            analysis.IsSupportSignal,
            analysis.IsFactualSignal,
            analysis.IsCaptureSignal,
            analysis.ShouldReopen,
            analysis.AnswersPreviousQuestion,
            decision.OverallConfidence);

        return new EngageConversationContext(session, recentMessages, userMessage, decision, lastQuestion, knowledgeSummary, analysis);
    }

    private EngageAnalysisSummary BuildAnalysisSummary(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        string knowledgeSummary,
        AiDecisionContract decision)
    {
        var trimmedMessage = userMessage.Trim();
        var normalizedMessage = new EngageInputInterpreter().NormalizeUserMessage(userMessage);
        var assistantMessages = recentMessages.Count(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        var isInitialTurn = assistantMessages == 0;
        var answersPreviousQuestion = !string.IsNullOrWhiteSpace(trimmedMessage)
            && trimmedMessage.Length <= 120
            && !trimmedMessage.Contains('?', StringComparison.Ordinal)
            && !normalizedMessage.StartsWith("what ", StringComparison.Ordinal)
            && !normalizedMessage.StartsWith("how ", StringComparison.Ordinal);

        var aiSuggestedCapture = decision.Recommendations?.Any(item =>
            item.ProposedCommand is { Count: > 0 } proposed
            && (proposed.ContainsKey("captureGoal")
                || proposed.ContainsKey("captureType")
                || proposed.ContainsKey("captureLocation")
                || proposed.ContainsKey("capturedName")
                || proposed.ContainsKey("capturedPreferredContactMethod"))) == true;

        var isClose = _policy.IsConversationCloseSignal(userMessage);
        var isSupport = _policy.IsExplicitEscalationRequest(userMessage) || _policy.NeedsHumanHelp(userMessage);
        var isFactual = _policy.IsServiceQuestion(userMessage)
                        || trimmedMessage.Contains('?', StringComparison.Ordinal)
                        || (!string.IsNullOrWhiteSpace(knowledgeSummary) && normalizedMessage.Contains("pricing", StringComparison.Ordinal));
        var isCapture = _policy.IsStrongCommercialIntent(userMessage) || _policy.IsExplicitCommercialContactRequest(userMessage) || aiSuggestedCapture;
        var shouldReopen = _policy.ShouldReopenCompletedConversation(session, userMessage);

        var likelyStateHint = isInitialTurn
            ? "Greeting"
            : isCapture
                ? "CaptureLead"
                : "Discover";

        return new EngageAnalysisSummary(
            likelyStateHint,
            answersPreviousQuestion,
            aiSuggestedCapture,
            decision.OverallConfidence,
            isInitialTurn,
            isClose,
            isSupport,
            isFactual,
            isCapture,
            shouldReopen);
    }
}

public sealed class EngageConversationContext
{
    public EngageChatSession Session { get; }
    public IReadOnlyCollection<EngageChatMessage> RecentMessages { get; }
    public string UserMessage { get; }
    public AiDecisionContract AiDecision { get; }
    public string? LastAssistantQuestion { get; }
    public string KnowledgeSummary { get; }
    public EngageAnalysisSummary Analysis { get; }
    public EngageNextActionDecision? PrimaryActionDecision { get; private set; }

    public EngageConversationContext(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        AiDecisionContract aiDecision,
        string? lastQuestion,
        string knowledgeSummary,
        EngageAnalysisSummary analysis)
    {
        Session = session;
        RecentMessages = recentMessages;
        UserMessage = userMessage;
        AiDecision = aiDecision;
        LastAssistantQuestion = lastQuestion;
        KnowledgeSummary = knowledgeSummary;
        Analysis = analysis;
    }

    public void SetPrimaryAction(EngageNextActionDecision decision)
    {
        PrimaryActionDecision = decision;
    }
}

public sealed record EngageAnalysisSummary(
    string LikelyStateHint,
    bool AnswersPreviousQuestion,
    bool AiSuggestedCapture,
    decimal OverallConfidence,
    bool IsInitialTurn,
    bool IsCloseSignal,
    bool IsSupportSignal,
    bool IsFactualSignal,
    bool IsCaptureSignal,
    bool ShouldReopen);
