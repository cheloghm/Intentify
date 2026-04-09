namespace Intentify.Modules.Engage.Application;

public sealed class EngageSmalltalkSignalMatcher
{
    // If any of these appear in the message, it's business intent — never treat as smalltalk
    private static readonly string[] BusinessIntentKeywords =
    [
        "website", "service", "services", "help", "need", "want", "looking",
        "price", "cost", "quote", "contact", "hire", "build", "develop",
        "design", "cybersecurity", "security", "software", "cloud", " it ",
        "consulting", "business", "company", "solution", "support", "interested",
        "information", "info", "how much", "what do you", "can you", "do you"
    ];

    private readonly EngageInputInterpreter _inputInterpreter;

    public EngageSmalltalkSignalMatcher(EngageInputInterpreter inputInterpreter)
    {
        _inputInterpreter = inputInterpreter;
    }

    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
    {
        var normalized = message.Trim().ToLowerInvariant();

        // Business intent overrides all smalltalk logic — must go to AI
        foreach (var keyword in BusinessIntentKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.Ordinal))
            {
                response = string.Empty;
                return false;
            }
        }
        var isGreeting = normalized is "hi" or "hello" or "hey" || _inputInterpreter.IsLikelyGreetingTypo(normalized);
        var isAcknowledgement = normalized is "yes" or "no" or "ok" or "okay" or "thanks" or "thank you" or "sure";
        var isContinuation = IsContinuationReply(normalized);
        var isVeryShortNonQuestion = normalized.Length > 0 && normalized.Length <= 5 && !normalized.Contains('?');

        if (priorAssistantAskedQuestion && (isAcknowledgement || isContinuation || isVeryShortNonQuestion))
        {
            response = string.Empty;
            return false;
        }

        if (!isGreeting && !isAcknowledgement && !isVeryShortNonQuestion)
        {
            response = string.Empty;
            return false;
        }

        response = isGreeting ? greetingResponse : ackResponse;
        return true;
    }

    public bool IsContinuationReply(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return EngageContinuationPhraseBank.ContinuationPhrases.Contains(normalized, StringComparer.Ordinal);
    }
}
