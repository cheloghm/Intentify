using System.Text.RegularExpressions;
using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageConversationPolicy
{
    private const string ContactDetailsNamePrefix = "my name is";
    private static readonly string[] HumanHelpPhrases =
    [
        "contact form",
        "form isn't working",
        "form is not working",
        "can't submit",
        "cannot submit",
        "doesn't submit"
    ];
    private static readonly string[] HumanHelpRequestPhrases =
    [
        "help me",
        "need help",
        "someone help",
        "talk to",
        "speak to",
        "human",
        "agent",
        "representative",
        "support"
    ];
    private static readonly string[] HumanHelpProblemTerms =
    [
        "broken",
        "error",
        "failed",
        "not working",
        "doesn't work",
        "checkout",
        "payment",
        "refund",
        "complaint",
        "issue",
        "problem"
    ];
    private static readonly string[] CommercialIntentTopicTerms =
    [
        "project",
        "remodel",
        "renovation",
        "installation",
        "service"
    ];
    private static readonly string[] CommercialIntentActionTerms =
    [
        "looking for",
        "looking to",
        "need",
        "quote",
        "estimate",
        "pricing"
    ];
    private static readonly string[] ContinuationPhrases =
    [
        "yes please",
        "go ahead",
        "that's fine",
        "thats fine",
        "that’s fine",
        "sounds good",
        "okay then"
    ];
    private static readonly string[] GreetingTypos =
    [
        "hllo",
        "helo",
        "hy",
        "helllo",
        "helloo",
        "heloo"
    ];
    private static readonly string[] RecommendationPhrases =
    [
        "which one is better",
        "which one should i pick",
        "what do you recommend",
        "which should i choose",
        "which color should i pick",
        "which spec is best",
        "what should i choose",
        "recommend"
    ];
    private static readonly string[] ExplicitEscalationTerms =
    [
        "talk to a human",
        "speak to a human",
        "human support",
        "contact support",
        "call me",
        "call back",
        "callback",
        "reach out"
    ];

    public bool TryBuildSmalltalkResponse(string message, bool priorAssistantAskedQuestion, string greetingResponse, string ackResponse, out string response)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var isGreeting = normalized is "hi" or "hello" or "hey" || IsLikelyGreetingTypo(normalized);
        var isAcknowledgement = normalized is "yes" or "no" or "ok" or "okay" or "thanks" or "thank you" or "sure";
        var isContinuation = IsContinuationReply(normalized);
        var isVeryShortNonQuestion = normalized.Length > 0 && normalized.Length <= 5 && !normalized.Contains('?');

        if (priorAssistantAskedQuestion && (isAcknowledgement || isContinuation))
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
        return ContinuationPhrases.Contains(normalized, StringComparer.Ordinal);
    }

    public bool IsStrongCommercialIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var hasAction = CommercialIntentActionTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasTopic = CommercialIntentTopicTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal))
            || normalized.Contains("help with", StringComparison.Ordinal)
            || normalized.Contains("for my", StringComparison.Ordinal)
            || normalized.Contains("for our", StringComparison.Ordinal);
        return hasAction && hasTopic;
    }

    public bool IsExplicitCommercialContactRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        var asksForContact = normalized.Contains("contact", StringComparison.Ordinal)
            || normalized.Contains("call", StringComparison.Ordinal)
            || normalized.Contains("callback", StringComparison.Ordinal)
            || normalized.Contains("call back", StringComparison.Ordinal)
            || normalized.Contains("reach out", StringComparison.Ordinal);
        var asksForQuote = normalized.Contains("quote", StringComparison.Ordinal)
            || normalized.Contains("estimate", StringComparison.Ordinal);
        return asksForContact || asksForQuote;
    }

    public bool IsRecommendationIntent(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        return RecommendationPhrases.Any(phrase => normalizedMessage.Contains(phrase, StringComparison.Ordinal));
    }

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
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            return "What outcome are you trying to achieve?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureType))
        {
            return "What type of work or use case is this for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            return "What location should we plan for?";
        }

        if (string.IsNullOrWhiteSpace(session.CaptureConstraints))
        {
            return "Any key constraints like budget or timeline?";
        }

        return "Please share your first name and best email so our team can follow up.";
    }

    public bool NeedsHumanHelp(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (HumanHelpPhrases.Any(phrase => message.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var normalized = message.Trim().ToLowerInvariant();
        var requestedHumanHelp = HumanHelpRequestPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
        if (!requestedHumanHelp)
        {
            return false;
        }

        return HumanHelpProblemTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    public bool ShouldAttemptSupportTroubleshoot(EngageChatSession session, string message, bool isSupportCaptureMode)
    {
        if (IsExplicitEscalationRequest(message))
        {
            return false;
        }

        return !string.Equals(session.ConversationState, "SupportTriage", StringComparison.Ordinal)
            && !isSupportCaptureMode;
    }

    public ChatIntent DetectIntent(string message)
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

    public bool ShouldEscalateFallback(EngageBot bot, ChatIntent intent, string userMessage, string reason, bool isRealQuestion)
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
            "professional" => "I can help with that — which part should we focus on first?",
            "casual" => "Sure — what are you trying to do right now?",
            _ => defaultResponse
        };
    }

    public bool TryBuildCommercialIntentContactPrompt(string message, string prefix, out string prompt)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            prompt = string.Empty;
            return false;
        }

        var hasTopic = CommercialIntentTopicTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasAction = CommercialIntentActionTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasFirstPartySignal = normalized.StartsWith("i ", StringComparison.Ordinal)
            || normalized.Contains(" i ", StringComparison.Ordinal)
            || normalized.StartsWith("we ", StringComparison.Ordinal)
            || normalized.Contains(" we ", StringComparison.Ordinal)
            || normalized.Contains(" my ", StringComparison.Ordinal)
            || normalized.Contains(" our ", StringComparison.Ordinal)
            || normalized.Contains("looking to", StringComparison.Ordinal);

        if (!(hasTopic && hasAction && hasFirstPartySignal))
        {
            prompt = string.Empty;
            return false;
        }

        var condensedNeed = message.Trim().TrimEnd('.', '!', '?');
        if (condensedNeed.Length > 96)
        {
            condensedNeed = condensedNeed[..96].TrimEnd();
        }

        prompt = $"{prefix} \"{condensedNeed}\". I can get this moving — what’s your first name?";
        return true;
    }

    public string? TryExtractEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractPhone(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"(?:\+?\d[\d\-\.\(\)\s]{6,}\d)");
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractName(string message, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var withoutEmail = !string.IsNullOrWhiteSpace(email)
            ? message.Replace(email, string.Empty, StringComparison.OrdinalIgnoreCase)
            : message;

        var withoutContact = !string.IsNullOrWhiteSpace(phone)
            ? withoutEmail.Replace(phone, string.Empty, StringComparison.OrdinalIgnoreCase)
            : withoutEmail;

        var normalized = withoutContact.Trim(' ', ',', '.', ';', ':', '-', '_');
        if (normalized.StartsWith(ContactDetailsNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[ContactDetailsNamePrefix.Length..].Trim(' ', ',', '.', ';', ':', '-', '_');
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.Length <= 200 ? normalized : normalized[..200];
    }

    private static bool IsLikelyGreetingTypo(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        return GreetingTypos.Contains(normalizedMessage, StringComparer.Ordinal);
    }

    private bool IsExplicitEscalationRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        if (ExplicitEscalationTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
        {
            return true;
        }

        var containsHumanTarget = normalized.Contains("human", StringComparison.Ordinal)
            || normalized.Contains("agent", StringComparison.Ordinal)
            || normalized.Contains("representative", StringComparison.Ordinal)
            || normalized.Contains("support", StringComparison.Ordinal);

        var containsEscalationVerb = normalized.Contains("talk", StringComparison.Ordinal)
            || normalized.Contains("speak", StringComparison.Ordinal)
            || normalized.Contains("contact", StringComparison.Ordinal)
            || normalized.Contains("connect", StringComparison.Ordinal)
            || normalized.Contains("call", StringComparison.Ordinal);

        return containsHumanTarget && containsEscalationVerb;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
