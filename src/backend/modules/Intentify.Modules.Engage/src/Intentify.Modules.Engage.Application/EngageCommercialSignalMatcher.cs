namespace Intentify.Modules.Engage.Application;

public sealed class EngageCommercialSignalMatcher
{
    public bool IsStrongCommercialIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var hasAction = EngageCommercialSignalBank.IntentActionTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasTopic = EngageCommercialSignalBank.IntentTopicTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal))
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
        var asksForContact = EngageCommercialSignalBank.ExplicitContactTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var asksForQuote = EngageCommercialSignalBank.ExplicitQuoteTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        return asksForContact || asksForQuote;
    }

    public bool IsRecommendationIntent(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        return EngageCommercialSignalBank.RecommendationPhrases.Any(phrase => normalizedMessage.Contains(phrase, StringComparison.Ordinal));
    }

    public bool TryBuildCommercialIntentContactPrompt(string message, string prefix, out string prompt)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            prompt = string.Empty;
            return false;
        }

        var hasTopic = EngageCommercialSignalBank.IntentTopicTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
        var hasAction = EngageCommercialSignalBank.IntentActionTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
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
