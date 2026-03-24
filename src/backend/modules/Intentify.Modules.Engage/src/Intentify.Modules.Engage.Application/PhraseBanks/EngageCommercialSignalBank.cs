namespace Intentify.Modules.Engage.Application;

internal static class EngageCommercialSignalBank
{
    internal static readonly string[] IntentTopicTerms =
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

    internal static readonly string[] IntentActionTerms =
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

    internal static readonly string[] ExplicitContactTerms =
    [
        "contact",
        "call",
        "callback",
        "call back",
        "reach out"
    ];

    internal static readonly string[] ExplicitQuoteTerms =
    [
        "quote",
        "estimate"
    ];

    internal static readonly string[] RecommendationPhrases =
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
}
