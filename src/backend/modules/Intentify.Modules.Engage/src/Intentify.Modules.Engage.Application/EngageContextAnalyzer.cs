using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;   // for OperationResult<T> and ChatSendResult
using Intentify.Modules.Sites.Application; // for Site in Orchestrator
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

        // Use normal string concatenation to avoid raw string brace issues
        var prompt = "You are a world-class sales rep for " + bot.Tone + " tone businesses.\n" +
                     "Current state: " + session.ConversationState + "\n" +
                     "Captured slots: Goal=" + (session.CaptureGoal ?? "none") + 
                     ", Type=" + (session.CaptureType ?? "none") + 
                     ", Location=" + (session.CaptureLocation ?? "none") + "\n" +
                     "Last question: " + (lastQuestion ?? "none") + "\n" +
                     "Knowledge: " + (knowledgeSummary.Length > 300 ? knowledgeSummary[..300] : knowledgeSummary) + "\n" +
                     "User said: \"" + userMessage + "\"\n\n" +
                     "Return ONLY valid JSON with keys: recommendedState, slotFills (object), intentConfidence, nextBestAction.";

        // TODO: Replace with real AiDecisionGenerationService call once you have the bundle logic
        var decision = new AiDecisionContract 
        { 
            RecommendedState = "Discover", 
            // Fill other properties as needed from your existing service
        };

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
    public string RecommendedState => AiDecision?.RecommendedState ?? "Discover";

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
