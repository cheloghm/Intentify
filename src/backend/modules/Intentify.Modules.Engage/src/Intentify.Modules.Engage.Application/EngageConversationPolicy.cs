using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageConversationPolicy
{
    private static readonly EngageInputInterpreter InputInterpreter = new();
    private static readonly EngageSmalltalkSignalMatcher SmalltalkSignals = new(InputInterpreter);
    private static readonly EngageSupportSignalMatcher SupportSignals = new(InputInterpreter);
    private static readonly EngageCommercialSignalMatcher CommercialSignals = new();

    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
        => SmalltalkSignals.TryBuildSmalltalkResponse(message, priorAssistantAskedQuestion, greetingResponse, ackResponse, out response);

    public bool IsContinuationReply(string message)
        => SmalltalkSignals.IsContinuationReply(message);

    public bool IsStrongCommercialIntent(string message)
        => CommercialSignals.IsStrongCommercialIntent(message);

    public bool IsExplicitCommercialContactRequest(string message)
        => CommercialSignals.IsExplicitCommercialContactRequest(message);

    public bool IsRecommendationIntent(string normalizedMessage)
        => CommercialSignals.IsRecommendationIntent(normalizedMessage);

    public string BuildRecommendationResponse(EngageChatSession session, string message)
    {
        if (HasSufficientDiscoveryContext(session))
        {
            return "Based on what you’ve shared, I recommend the option that best aligns with your goal and constraints.";
        }

        return message.Contains("color", StringComparison.OrdinalIgnoreCase)
            ? "Happy to help — where will this color be used, and do you want a safer neutral look or a bolder standout look?"
            : "Happy to help — what matters most for this choice: budget, speed, performance, or simplicity?";
    }

    public int ComputeCommercialIntentScore(EngageChatSession session)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureType))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(session.CapturedName))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod))
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(session.CapturedEmail) || !string.IsNullOrWhiteSpace(session.CapturedPhone))
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    public string BuildCommercialOpportunityLabel(int intentScore)
    {
        return intentScore switch
        {
            >= 80 => "HighIntentCommercialOpportunity",
            >= 60 => "QualifiedCommercialOpportunity",
            _ => "CommercialOpportunity"
        };
    }

    public string BuildCommercialOpportunitySummary(EngageChatSession session)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            parts.Add($"Goal: {session.CaptureGoal}");
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureType))
        {
            parts.Add($"Type: {session.CaptureType}");
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            parts.Add($"Location: {session.CaptureLocation}");
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            parts.Add($"Constraints: {session.CaptureConstraints}");
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(session.CaptureContext))
        {
            parts.Add($"Context: {session.CaptureContext}");
        }

        return parts.Count == 0
            ? "Commercial inquiry captured from Engage conversation."
            : string.Join("; ", parts);
    }

    public string BuildSuggestedFollowUpMessage(EngageChatSession session)
    {
        var namePrefix = string.IsNullOrWhiteSpace(session.CapturedName)
            ? "Hi"
            : $"Hi {session.CapturedName}";

        var goalContext = string.IsNullOrWhiteSpace(session.CaptureGoal)
            ? "your project"
            : session.CaptureGoal;

        return $"{namePrefix}, thanks for reaching out about {goalContext}. We reviewed your request and can share tailored next steps—what timing works best for a quick follow-up?";
    }

    public bool HasSufficientDiscoveryContext(EngageChatSession session)
    {
        var scopedFields = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            scopedFields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureType))
        {
            scopedFields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            scopedFields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            scopedFields++;
        }

        return scopedFields >= 2;
    }

    public string BuildNextDiscoveryQuestion(EngageChatSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CaptureType)
            && string.IsNullOrWhiteSpace(session.CaptureGoal)
            && HasProjectIntentContext(session))
        {
            return "What kind of business is this for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            return "What are you trying to achieve first?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureType))
        {
            return "What kind of business is this for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            return "What location should we plan for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            if (TryBuildBusinessAwareConstraintQuestion(session, out var refinementQuestion))
            {
                return refinementQuestion;
            }

            return "Any key constraints like budget or timeline?";
        }

        return "Thanks — that gives me enough context. If you want tailored options and next steps, share your first name and best email.";
    }

    public bool IsCommercialCaptureReady(EngageChatSession session, bool explicitContactRequest)
    {
        if (explicitContactRequest)
        {
            return true;
        }

        var fields = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            fields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureType))
        {
            fields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            fields++;
        }

        if (!string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            fields++;
        }

        return fields >= 3;
    }

    public bool NeedsHumanHelp(string message)
        => SupportSignals.NeedsHumanHelp(message);

    public bool IsAlreadyToldYouSignal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        return EngageContextRecoveryPhraseBank.AlreadyToldYouPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
    }

    public bool ShouldAttemptSupportTroubleshoot(EngageChatSession session, string message, bool isSupportCaptureMode)
    {
        if (SupportSignals.IsExplicitEscalationRequest(message))
        {
            return false;
        }

        return !string.Equals(session.ConversationState, "SupportTriage", StringComparison.Ordinal)
            && !isSupportCaptureMode;
    }

    internal ChatIntent DetectIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();

        if (normalized.Length <= 3 || normalized is "help" or "info" or "details" or "price")
        {
            return ChatIntent.AmbiguousShortPrompt;
        }

        var containsHumanTarget = normalized.Contains("human", StringComparison.Ordinal)
            || normalized.Contains("agent", StringComparison.Ordinal)
            || normalized.Contains("representative", StringComparison.Ordinal)
            || normalized.Contains("person", StringComparison.Ordinal);
        var containsHandoffVerb = normalized.Contains("need", StringComparison.Ordinal)
            || normalized.Contains("want", StringComparison.Ordinal)
            || normalized.Contains("speak", StringComparison.Ordinal)
            || normalized.Contains("talk", StringComparison.Ordinal)
            || normalized.Contains("connect", StringComparison.Ordinal)
            || normalized.Contains("help", StringComparison.Ordinal);

        if (containsHumanTarget && containsHandoffVerb)
        {
            return ChatIntent.EscalationHelp;
        }

        if (normalized.Contains("contact", StringComparison.Ordinal)
            || normalized.Contains("phone", StringComparison.Ordinal)
            || normalized.Contains("email", StringComparison.Ordinal)
            || normalized.Contains("call", StringComparison.Ordinal))
        {
            return ChatIntent.Contact;
        }

        if (normalized.Contains("location", StringComparison.Ordinal)
            || normalized.Contains("address", StringComparison.Ordinal)
            || normalized.Contains("where", StringComparison.Ordinal)
            || normalized.Contains("located", StringComparison.Ordinal))
        {
            return ChatIntent.Location;
        }

        if (normalized.Contains("hours", StringComparison.Ordinal)
            || normalized.Contains("open", StringComparison.Ordinal)
            || normalized.Contains("close", StringComparison.Ordinal)
            || normalized.Contains("time", StringComparison.Ordinal))
        {
            return ChatIntent.Hours;
        }

        if (normalized.Contains("service", StringComparison.Ordinal)
            || normalized.Contains("menu", StringComparison.Ordinal)
            || normalized.Contains("offer", StringComparison.Ordinal)
            || normalized.Contains("pricing", StringComparison.Ordinal)
            || normalized.Contains("order", StringComparison.Ordinal))
        {
            return ChatIntent.Services;
        }

        if (normalized.Contains("org", StringComparison.Ordinal)
            || normalized.Contains("organization", StringComparison.Ordinal)
            || normalized.Contains("business name", StringComparison.Ordinal)
            || normalized.Contains("company name", StringComparison.Ordinal)
            || normalized.Contains("name of", StringComparison.Ordinal))
        {
            return ChatIntent.Organization;
        }

        return ChatIntent.General;
    }

    internal bool ShouldEscalateFallback(EngageBot bot, ChatIntent intent, string userMessage, string reason, bool isRealQuestion)
    {
        var isActionableHelpIntent = intent == ChatIntent.EscalationHelp || NeedsHumanHelp(userMessage);

        if (string.Equals(NormalizeOptional(bot.FallbackStyle), "handoff", StringComparison.OrdinalIgnoreCase)
            && (isActionableHelpIntent || isRealQuestion))
        {
            return true;
        }

        if (reason == "AiUnavailable" && isRealQuestion)
        {
            return true;
        }

        return isActionableHelpIntent;
    }

    public string BuildSoftFallbackResponse(EngageBot bot, string defaultResponse)
    {
        var tone = NormalizeOptional(bot.Tone)?.ToLowerInvariant();
        return tone switch
        {
            "professional" => "Happy to help — which part should we focus on first?",
            "casual" => "Happy to help — what are you trying to do right now?",
            _ => defaultResponse
        };
    }

    public bool TryBuildCommercialIntentContactPrompt(string message, string prefix, out string prompt)
        => CommercialSignals.TryBuildCommercialIntentContactPrompt(message, prefix, out prompt);

    public string NormalizeUserMessage(string message) => InputInterpreter.NormalizeUserMessage(message);

    public string? TryExtractEmail(string message)
        => InputInterpreter.TryExtractEmail(message);

    public string? TryExtractPhone(string message)
        => InputInterpreter.TryExtractPhone(message);

    public string? TryExtractName(string message, string? email, string? phone)
        => InputInterpreter.TryExtractName(message, email, phone);

    public string? TryExtractPreferredContactMethod(string message, string? email, string? phone)
        => InputInterpreter.TryExtractPreferredContactMethod(message, email, phone);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryBuildBusinessAwareConstraintQuestion(EngageChatSession session, out string question)
    {
        var context = $"{session.CaptureGoal} {session.CaptureType} {session.CaptureContext}".ToLowerInvariant();
        var isDigitalContext = context.Contains("website", StringComparison.Ordinal)
            || context.Contains("site", StringComparison.Ordinal)
            || context.Contains("online store", StringComparison.Ordinal)
            || context.Contains("ecommerce", StringComparison.Ordinal)
            || context.Contains("e-commerce", StringComparison.Ordinal);

        if (isDigitalContext)
        {
            question = "Any key constraints like budget, timeline, or systems this needs to work with?";
            return true;
        }

        var isBookingContext = context.Contains("booking", StringComparison.Ordinal)
            || context.Contains("appointment", StringComparison.Ordinal)
            || context.Contains("reservation", StringComparison.Ordinal)
            || context.Contains("schedule", StringComparison.Ordinal);
        if (isBookingContext)
        {
            question = "Any key constraints like budget, timeline, or scheduling requirements?";
            return true;
        }

        var isCommerceContext = context.Contains("retail", StringComparison.Ordinal)
            || context.Contains("restaurant", StringComparison.Ordinal)
            || context.Contains("food", StringComparison.Ordinal)
            || context.Contains("drink", StringComparison.Ordinal)
            || context.Contains("menu", StringComparison.Ordinal)
            || context.Contains("inventory", StringComparison.Ordinal)
            || context.Contains("order", StringComparison.Ordinal);
        if (isCommerceContext)
        {
            question = "Any key constraints like budget, timeline, or fulfillment capacity?";
            return true;
        }

        question = string.Empty;
        return false;
    }

    private static bool HasProjectIntentContext(EngageChatSession session)
    {
        var context = $"{session.CaptureGoal} {session.CaptureType} {session.CaptureContext}".ToLowerInvariant();
        return context.Contains("website", StringComparison.Ordinal)
            || context.Contains("site", StringComparison.Ordinal)
            || context.Contains("seo", StringComparison.Ordinal)
            || context.Contains("quote", StringComparison.Ordinal)
            || context.Contains("project", StringComparison.Ordinal)
            || context.Contains("redesign", StringComparison.Ordinal)
            || context.Contains("build", StringComparison.Ordinal)
            || context.Contains("booking", StringComparison.Ordinal);
    }
}
