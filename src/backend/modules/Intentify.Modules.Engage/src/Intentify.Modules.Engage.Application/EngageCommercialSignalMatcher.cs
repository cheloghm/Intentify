namespace Intentify.Modules.Engage.Application;

public sealed class EngageCommercialSignalMatcher
{
    private static readonly string[] CommercialIntentTopicTerms =
    [
        "project",
        "remodel",
        "renovation",
        "installation",
        "service",
        "solution",
        "software",
        "app",
        "platform",
        "integration",
        "website",
        "store",
        "shop",
        "restaurant",
        "menu",
        "order",
        "booking",
        "appointment",
        "campaign",
        "consulting",
        "package",
        "plan"
    ];

    private static readonly string[] CommercialIntentActionTerms =
    [
        "looking for",
        "looking to",
        "need",
        "quote",
        "estimate",
        "pricing",
        "buy",
        "purchase",
        "book",
        "schedule",
        "hire",
        "start",
        "launch",
        "upgrade",
        "improve",
        "set up",
        "setup"
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

    public bool IsStrongCommercialIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var hasAction = CommercialIntentActionTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasTopic = CommercialIntentTopicTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal))
            || normalized.Contains("help with", StringComparison.Ordinal)
            || normalized.Contains("for my", StringComparison.Ordinal)
            || normalized.Contains("for our", StringComparison.Ordinal)
            || normalized.Contains("for my business", StringComparison.Ordinal)
            || normalized.Contains("for our business", StringComparison.Ordinal)
            || normalized.Contains("customers", StringComparison.Ordinal)
            || normalized.Contains("clients", StringComparison.Ordinal);
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
}
