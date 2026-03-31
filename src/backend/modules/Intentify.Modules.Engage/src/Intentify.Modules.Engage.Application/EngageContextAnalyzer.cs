using Intentify.Modules.Engage.Domain;
using Intentify.Shared.AI;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageContextAnalyzer
{
    private readonly AiDecisionGenerationService _aiDecisionService;

    public EngageContextAnalyzer(
        AiDecisionGenerationService aiDecisionService,
        ILogger<EngageContextAnalyzer> logger)
    {
        _aiDecisionService = aiDecisionService;
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
        _ = BuildDistilledHistory(recentMessages, userMessage);
        var lastQuestion = recentMessages
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                     && m.Content.Trim().EndsWith("?"))
            .Select(m => m.Content.Trim())
            .LastOrDefault();

        var decision = visitorBundle != null
            ? await _aiDecisionService.GenerateAsync(visitorBundle, ct) 
            : new AiDecisionContract(
                "1.0",                     // SchemaVersion (adjust if your constructor differs)
                "Discover",                // Default decision
                null,                      // ContextRef
                0.5m,                      // Confidence
                null,                      // Recommendations
                AiDecisionValidationStatus.Valid,
                null,                      // Errors
                null,                      // RecommendationTypes
                false,                     // ShouldFallback
                null,                      // NextBestAction
                null);                     // ExtraData

        var analysis = BuildAnalysisSummary(session, recentMessages, userMessage, decision);

        return new EngageConversationContext(session, recentMessages, userMessage, decision, lastQuestion, knowledgeSummary, analysis);
    }

    private static string BuildDistilledHistory(IReadOnlyCollection<EngageChatMessage> messages, string currentMessage)
    {
        return string.Join("\n", messages.TakeLast(12)
            .Select(m => $"{m.Role}: {m.Content.Trim()}"));
    }

    private static EngageAnalysisSummary BuildAnalysisSummary(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        AiDecisionContract decision)
    {
        var trimmedMessage = userMessage.Trim();
        var normalizedMessage = new EngageInputInterpreter().NormalizeUserMessage(userMessage);
        var assistantMessages = recentMessages.Count(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        var isInitialTurn = assistantMessages == 0;
        var looksLikePivotQuestion = trimmedMessage.Contains('?', StringComparison.Ordinal)
            || normalizedMessage.Contains("services", StringComparison.Ordinal)
            || normalizedMessage.Contains("pricing", StringComparison.Ordinal)
            || normalizedMessage.Contains("cost", StringComparison.Ordinal);
        var answersPreviousQuestion = !string.IsNullOrWhiteSpace(trimmedMessage)
            && trimmedMessage.Length <= 80
            && !looksLikePivotQuestion;

        var aiSuggestedCapture = decision.Recommendations?.Any(item =>
            item.ProposedCommand is { Count: > 0 } proposed
            && (proposed.ContainsKey("captureGoal")
                || proposed.ContainsKey("captureType")
                || proposed.ContainsKey("captureLocation")
                || proposed.ContainsKey("capturedName")
                || proposed.ContainsKey("capturedPreferredContactMethod"))) == true;

        var likelyStateHint = isInitialTurn
            ? "Greeting"
            : string.Equals(session.ConversationState, "CaptureLead", StringComparison.Ordinal)
                ? "CaptureLead"
                : aiSuggestedCapture
                    ? "CaptureLead"
                    : "Discover";

        return new EngageAnalysisSummary(
            likelyStateHint,
            answersPreviousQuestion,
            aiSuggestedCapture,
            decision.OverallConfidence,
            isInitialTurn);
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
    bool IsInitialTurn);
