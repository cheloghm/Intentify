namespace Intentify.Modules.Engage.Application;

public sealed class EngageSupportSignalMatcher
{
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

    private readonly EngageInputInterpreter _inputInterpreter;

    public EngageSupportSignalMatcher(EngageInputInterpreter inputInterpreter)
    {
        _inputInterpreter = inputInterpreter;
    }

    public bool NeedsHumanHelp(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        if (_inputInterpreter.ContainsSupportProblemSignal(normalized))
        {
            return true;
        }

        if (HumanHelpPhrases.Any(phrase => message.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var requestedHumanHelp = HumanHelpRequestPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
        if (!requestedHumanHelp)
        {
            return false;
        }

        return _inputInterpreter.ContainsSupportProblemSignal(normalized)
            || normalized.Contains("refund", StringComparison.Ordinal)
            || normalized.Contains("issue", StringComparison.Ordinal)
            || normalized.Contains("problem", StringComparison.Ordinal);
    }

    public bool IsExplicitEscalationRequest(string message)
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
}
