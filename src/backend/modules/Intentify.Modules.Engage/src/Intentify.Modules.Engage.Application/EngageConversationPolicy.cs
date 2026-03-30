using Intentify.Modules.Engage.Domain;
using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageConversationPolicy
{
    private static readonly EngageInputInterpreter _interpreter = new();
    private static readonly EngageSmalltalkSignalMatcher _smalltalk = new(_interpreter);
    private static readonly EngageSupportSignalMatcher _support = new(_interpreter);
    private static readonly EngageCommercialSignalMatcher _commercial = new();

    // All your original methods kept exactly as they were in the repo
    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
        => _smalltalk.TryBuildSmalltalkResponse(message, priorAssistantAskedQuestion, greetingResponse, ackResponse, out response);

    public bool IsContinuationReply(string message) => _smalltalk.IsContinuationReply(message);
    public bool IsStrongCommercialIntent(string message) => _commercial.IsStrongCommercialIntent(message);
    public bool IsExplicitCommercialContactRequest(string message) => _commercial.IsExplicitCommercialContactRequest(message);
    public bool IsRecommendationIntent(string normalizedMessage) => _commercial.IsRecommendationIntent(normalizedMessage);
    public bool TryBuildCommercialIntentContactPrompt(string message, string prefix, out string prompt)
        => _commercial.TryBuildCommercialIntentContactPrompt(message, prefix, out prompt);

    public int ComputeCommercialIntentScore(EngageChatSession session)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) score += 25;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) score += 20;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) score += 15;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) score += 15;
        if (!string.IsNullOrWhiteSpace(session.CapturedName)) score += 10;
        if (!string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)) score += 5;
        if (!string.IsNullOrWhiteSpace(session.CapturedEmail) || !string.IsNullOrWhiteSpace(session.CapturedPhone)) score += 10;
        return Math.Clamp(score, 0, 100);
    }

    public bool HasSufficientDiscoveryContext(EngageChatSession session)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) count++;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) count++;
        return count >= 2;
    }

    public string BuildNextDiscoveryQuestion(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
            return "What are you trying to achieve first?";
        if (string.IsNullOrWhiteSpace(session.CaptureType))
            return "What kind of business is this for?";
        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
            return "What location should we plan for?";
        if (string.IsNullOrWhiteSpace(session.CaptureConstraints))
            return "Any key constraints like budget or timeline?";
        return "Thanks — that gives me enough context. Please share your first name and best contact method.";
    }

    public bool IsCommercialCaptureReady(EngageChatSession session, bool explicitContactRequest)
    {
        var fields = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) fields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) fields++;
        return explicitContactRequest || fields >= 3;
    }

    public bool NeedsHumanHelp(string message) => _support.NeedsHumanHelp(message);

    // Fixed & improved short-reply merging (no syntax errors)
    public bool TryMergeShortReplySlots(EngageChatSession session, string message, string? lastAssistantQuestion)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var normalized = _interpreter.NormalizeUserMessage(message);

        // Name
        if (lastAssistantQuestion != null && lastAssistantQuestion.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            var name = _interpreter.TryExtractName(message, null, null);
            if (!string.IsNullOrWhiteSpace(name))
            {
                session.CapturedName = name;
                return true;
            }
        }

        // Preferred contact method
        if (lastAssistantQuestion != null && (lastAssistantQuestion.Contains("email or phone", StringComparison.OrdinalIgnoreCase) || lastAssistantQuestion.Contains("reach you", StringComparison.OrdinalIgnoreCase)))
        {
            var method = _interpreter.TryExtractPreferredContactMethod(message, null, null);
            if (!string.IsNullOrWhiteSpace(method))
            {
                session.CapturedPreferredContactMethod = method;
                return true;
            }
        }

        // Discovery slots fallback
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            var match = Regex.Match(normalized, @"(need|want|looking for|trying to)\s+([^\.!\?,]+)", RegexOptions.IgnoreCase);
            if (match.Success) session.CaptureGoal = match.Groups[2].Value.Trim();
        }

        session.CaptureContext ??= normalized.Length > 200 ? normalized[..200] : normalized;
        return true;
    }

    public string BuildNaturalNextQuestion(EngageChatSession session, EngageConversationContext ctx)
    {
        return BuildNextDiscoveryQuestion(session); // reuse your logic, can be enhanced later with ctx.KnowledgeSummary
    }
}
