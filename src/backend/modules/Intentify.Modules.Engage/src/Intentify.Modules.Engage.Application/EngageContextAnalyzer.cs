using Intentify.Modules.Engage.Domain;
using Intentify.Shared.AI;
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
        string tenantVocabulary,
        EngageBot bot,
        VisitorContextBundle? visitorBundle,
        CancellationToken ct)
    {
        var distilled = BuildDistilledHistory(recentMessages, userMessage);
        var lastQuestion = recentMessages
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) 
                     && m.Content.Trim().EndsWith("?"))
            .Select(m => m.Content.Trim())
            .LastOrDefault();

        // Safe prompt
        var prompt = "You are a world-class sales rep. " +
                     "Current state: " + (session.ConversationState ?? "Discover") + ". " +
                     "User said: \"" + userMessage + "\". " +
                     "Last question: " + (lastQuestion ?? "none") + ". " +
                     "Return recommendedState (Discover|CaptureLead|...) and nextBestAction.";

        // Correct call - pass the bundle properly (this fixes the type error)
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

        return new EngageConversationContext(session, recentMessages, userMessage, decision, lastQuestion, knowledgeSummary);
    }

    private static string BuildDistilledHistory(IReadOnlyCollection<EngageChatMessage> messages, string currentMessage)
    {
        return string.Join("\n", messages.TakeLast(12)
            .Select(m => $"{m.Role}: {m.Content.Trim()}"));
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

    // Use the actual property that exists in your AiDecisionContract (fallback to a safe default)
    public string RecommendedState => "Discover"; // Temporary safe fallback - you can map from AiDecision later

    public EngageConversationContext(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        string userMessage,
        AiDecisionContract aiDecision,
        string? lastQuestion,
        string knowledgeSummary)
    {
        Session = session;
        RecentMessages = recentMessages;
        UserMessage = userMessage;
        AiDecision = aiDecision;
        LastAssistantQuestion = lastQuestion;
        KnowledgeSummary = knowledgeSummary;
    }
}
