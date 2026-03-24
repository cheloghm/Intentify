namespace Intentify.Modules.Engage.Application;

internal static class EngageSupportEscalationSignalBank
{
    internal static readonly string[] HumanHelpPhrases =
    [
        "contact form",
        "form isn't working",
        "form is not working",
        "can't submit",
        "cannot submit",
        "doesn't submit"
    ];

    internal static readonly string[] HumanHelpRequestPhrases =
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

    internal static readonly string[] ExplicitEscalationTerms =
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
}
