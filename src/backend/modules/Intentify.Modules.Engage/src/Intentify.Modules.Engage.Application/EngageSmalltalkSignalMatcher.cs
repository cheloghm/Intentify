namespace Intentify.Modules.Engage.Application;

public sealed class EngageSmalltalkSignalMatcher
{
    private readonly EngageInputInterpreter _inputInterpreter;

    public EngageSmalltalkSignalMatcher(EngageInputInterpreter inputInterpreter)
    {
        _inputInterpreter = inputInterpreter;
    }

    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
    {
        var normalized = message.Trim().ToLowerInvariant();
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
