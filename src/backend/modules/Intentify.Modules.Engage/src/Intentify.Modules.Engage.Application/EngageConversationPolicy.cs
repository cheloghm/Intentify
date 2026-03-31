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

        if (string.IsNullOrWhiteSpace(session.CapturedName))
            return "Thanks — that gives me enough context. Please share your first name.";

        var hasContact = !string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)
            || !string.IsNullOrWhiteSpace(session.CapturedEmail)
            || !string.IsNullOrWhiteSpace(session.CapturedPhone);
        if (!hasContact)
            return "Great, and what’s the best contact method for follow-up?";

        return "Thanks — I have what I need. Is there anything else you’d like help with?";
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
    public bool IsExplicitEscalationRequest(string message) => _support.IsExplicitEscalationRequest(message);

    // Fixed & improved short-reply merging (no syntax errors)
    public bool TryMergeShortReplySlots(EngageChatSession session, string message, string? lastAssistantQuestion)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var normalized = _interpreter.NormalizeUserMessage(message);
        var email = _interpreter.TryExtractEmail(message);
        var phone = _interpreter.TryExtractPhone(message);

        if (!string.IsNullOrWhiteSpace(email))
        {
            session.CapturedEmail = email;
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            session.CapturedPhone = phone;
        }

        var contactMethod = _interpreter.TryExtractPreferredContactMethod(message, email, phone);
        if (!string.IsNullOrWhiteSpace(contactMethod))
        {
            session.CapturedPreferredContactMethod = contactMethod;
        }

        // Name
        if (lastAssistantQuestion != null && lastAssistantQuestion.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            var name = _interpreter.TryExtractName(message, email, phone);
            if (!string.IsNullOrWhiteSpace(name))
            {
                session.CapturedName = name;
                return true;
            }
        }

        // Preferred contact method
        if (lastAssistantQuestion != null && (lastAssistantQuestion.Contains("email or phone", StringComparison.OrdinalIgnoreCase) || lastAssistantQuestion.Contains("reach you", StringComparison.OrdinalIgnoreCase)))
        {
            var method = _interpreter.TryExtractPreferredContactMethod(message, email, phone);
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

    public bool TryApplyStageContinuation(EngageChatSession session, string message, string? lastAssistantQuestion)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = _interpreter.NormalizeUserMessage(message);
        var trimmed = message.Trim();
        var updated = TryMergeShortReplySlots(session, message, lastAssistantQuestion);

        if (!string.IsNullOrWhiteSpace(lastAssistantQuestion))
        {
            if (lastAssistantQuestion.Contains("trying to achieve", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(session.CaptureGoal))
            {
                session.CaptureGoal = trimmed;
                updated = true;
            }

            if ((lastAssistantQuestion.Contains("kind of business", StringComparison.OrdinalIgnoreCase)
                 || lastAssistantQuestion.Contains("use case", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(session.CaptureType))
            {
                session.CaptureType = trimmed;
                updated = true;
            }

            if (lastAssistantQuestion.Contains("location", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(session.CaptureLocation))
            {
                session.CaptureLocation = trimmed;
                updated = true;
            }

            if ((lastAssistantQuestion.Contains("constraint", StringComparison.OrdinalIgnoreCase)
                 || lastAssistantQuestion.Contains("budget", StringComparison.OrdinalIgnoreCase)
                 || lastAssistantQuestion.Contains("timeline", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(session.CaptureConstraints))
            {
                session.CaptureConstraints = trimmed;
                updated = true;
            }
        }

        if (IsContextRecoverySignal(message))
        {
            return true;
        }

        if (normalized.Contains("just browsing", StringComparison.Ordinal)
            || normalized.Contains("maybe later", StringComparison.Ordinal)
            || normalized.Contains("not ready", StringComparison.Ordinal))
        {
            session.CaptureContext = string.IsNullOrWhiteSpace(session.CaptureContext)
                ? $"hesitation: {trimmed}"
                : $"{session.CaptureContext}; hesitation: {trimmed}";
            updated = true;
        }

        return updated;
    }

    public bool IsContextRecoverySignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = _interpreter.NormalizeUserMessage(message);
        return EngageContextRecoveryPhraseBank.AlreadyToldYouPhrases.Any(item =>
            normalized.Contains(item, StringComparison.Ordinal));
    }

    public bool IsNarrowObjectionSignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = _interpreter.NormalizeUserMessage(message);
        return normalized.Contains("too expensive", StringComparison.Ordinal)
            || normalized.Contains("not interested", StringComparison.Ordinal)
            || normalized.Contains("not now", StringComparison.Ordinal)
            || normalized.Contains("maybe later", StringComparison.Ordinal)
            || normalized.Contains("just browsing", StringComparison.Ordinal);
    }

    public bool IsClosureSignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = _interpreter.NormalizeUserMessage(message);
        return normalized is "no" or "no thanks" or "no thank you" or "thats all" or "that is all" or "nothing else" or "im good" or "i am good"
            || normalized.Contains("that will be all", StringComparison.Ordinal)
            || normalized.Contains("nothing else", StringComparison.Ordinal)
            || normalized.Contains("all good", StringComparison.Ordinal);
    }

    public string BuildClosureResponse(EngageChatSession session)
    {
        var hasLeadContact = !string.IsNullOrWhiteSpace(session.CapturedEmail)
            || !string.IsNullOrWhiteSpace(session.CapturedPhone)
            || !string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod);

        if (string.Equals(session.PendingCaptureMode, "Support", StringComparison.OrdinalIgnoreCase))
        {
            return "Thanks for the details — our support team will follow up shortly.";
        }

        if (hasLeadContact)
        {
            return "Perfect, thank you. We have your details and our team will reach out soon.";
        }

        return "Great, thanks for chatting with me today.";
    }

    public string BuildNarrowObjectionFollowUp(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            return "No pressure. What outcome would make this worth revisiting for you?";
        }

        return "Totally fair. What would need to be true for this to feel worth doing now?";
    }

    public string BuildContextRecoveryPrompt(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            return "Got it — I have your context so far. What are you trying to achieve first?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureType))
        {
            return $"You already shared your goal ({session.CaptureGoal}). What kind of business or use case is this for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            return $"I have your goal and use case noted. What location should we plan for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            return "Thanks — I have the basics. Any key constraints like budget or timeline?";
        }

        if (string.IsNullOrWhiteSpace(session.CapturedName))
        {
            return "Thanks — I have your details so far. Please share your first name.";
        }

        var hasContact = !string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)
            || !string.IsNullOrWhiteSpace(session.CapturedEmail)
            || !string.IsNullOrWhiteSpace(session.CapturedPhone);
        if (!hasContact)
        {
            return "Thanks — please share your preferred contact method.";
        }

        return "Thanks — I have your details so far. Is there anything else you’d like help with?";
    }

    public string BuildNaturalNextQuestion(EngageChatSession session, EngageConversationContext ctx)
    {
        return BuildNextDiscoveryQuestion(session); // reuse your logic, can be enhanced later with ctx.KnowledgeSummary
    }
}
