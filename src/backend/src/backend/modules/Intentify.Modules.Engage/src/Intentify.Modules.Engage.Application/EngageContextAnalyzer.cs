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
            .Where(m => m.Role == "assistant" && m.Content.Trim().EndsWith("?"))
            .Select(m => m.Content)
            .LastOrDefault();

        var prompt = $"""
You are a world-class sales rep for {bot.Tone} tone businesses.
Current state: {session.ConversationState}
Captured slots: Goal={session.CaptureGoal}, Type={session.CaptureType}, Location={session.CaptureLocation}, Constraints={session.CaptureConstraints}, Name={session.CapturedName}
Last question: {lastQuestion ?? "none"}
Knowledge: {knowledgeSummary}
Tenant vocab: {tenantVocabulary}
History: {distilled}

User said: "{userMessage}"

Return ONLY valid JSON:
{{
  "recommendedState": "Greeting|Discover|CaptureLead|CaptureSupport|Inform|Clarify|ConfirmHandoff",
  "slotFills": {{ "capturedName": "...", "captureGoal": "..." }},
  "intentConfidence": 0.85,
  "nextBestAction": "askNextDiscoveryQuestion|askName|askContactMethod|generateLeadTicket|..."
}}
""";

        var decision = await _aiDecisionService.GenerateAsync(/* bundle built from prompt */ , ct);
        return new EngageConversationContext(session, recentMessages, userMessage, decision, lastQuestion, knowledgeSummary);
    }

    private static string BuildDistilledHistory(IReadOnlyCollection<EngageChatMessage> messages, string currentMessage)
    {
        // Exact same distilled logic you already had in the old handler
        return string.Join("\n", messages.TakeLast(12).Select(m => $"{m.Role}: {m.Content}"));
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
    public string RecommendedState => AiDecision.RecommendedState ?? "Discover";

    public EngageConversationContext(EngageChatSession session, IReadOnlyCollection<EngageChatMessage> recent, string userMsg,
        AiDecisionContract decision, string? lastQ, string knowledge)
    {
        Session = session; RecentMessages = recent; UserMessage = userMsg;
        AiDecision = decision; LastAssistantQuestion = lastQ; KnowledgeSummary = knowledge;
    }
}
