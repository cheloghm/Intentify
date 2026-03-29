public sealed class EngageContextAnalyzer
{
    private readonly AiDecisionGenerationService _aiDecisionService;
    private readonly EngageConversationPolicy _policy;

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
        var distilledHistory = BuildDistilledHistory(recentMessages, userMessage);
        var lastQuestion = recentMessages.LastOrDefault(m => m.Role == "assistant" && m.Content.EndsWith("?"))?.Content;

        var prompt = $"""
You are an expert sales-rep context analyser for {bot.Tone} tone.
Session state: {session.ConversationState}
Captured: Goal={session.CaptureGoal}, Type={session.CaptureType}, Location={session.CaptureLocation}, Constraints={session.CaptureConstraints}, Name={session.CapturedName}, ContactMethod={session.CapturedPreferredContactMethod}
Last assistant question: {lastQuestion ?? "none"}
Knowledge summary: {knowledgeSummary}
Tenant vocabulary: {tenantVocabulary}
Distilled history: {distilledHistory}

User just said: "{userMessage}"

Return ONLY a JSON object with:
{{
  "recommendedState": "Discover|CaptureLead|... (one of the 7 states)",
  "slotFills": {{ "captureGoal": "...", "capturedName": "..." }},
  "intentConfidence": 0.0-1.0,
  "toneMatch": "warm/professional",
  "nextBestAction": "askName|askContactMethod|generateLead|..."
}}
""";

        var aiResult = await _aiDecisionService.GenerateAsync(/* bundle from prompt */ , ct);
        return new EngageConversationContext(session, recentMessages, userMessage, aiResult, lastQuestion, knowledgeSummary);
    }

    private static string BuildDistilledHistory(...) { /* existing distilled logic */ }
}
