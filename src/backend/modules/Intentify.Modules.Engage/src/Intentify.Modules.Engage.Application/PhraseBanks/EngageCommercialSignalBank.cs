namespace Intentify.Modules.Engage.Application;

internal static class EngageCommercialSignalBank
{
    internal static readonly string[] IntentTopicTerms =
    [
        // General business / services
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
        "system",
        "tool",
        "infrastructure",
        "automation",
        "workflow",
        "dashboard",
        "analytics",
        "reporting",
        "monitoring",

        // Digital / tech
        "website",
        "web app",
        "mobile app",
        "api",
        "cloud",
        "hosting",
        "saas",
        "ai",
        "machine learning",
        "data",
        "security",
        "cybersecurity",
        "devops",
        "kubernetes",
        "docker",
        "database",

        // Commerce / retail
        "store",
        "shop",
        "ecommerce",
        "product",
        "catalog",
        "inventory",
        "checkout",
        "payment",
        "subscription",
        "pricing",
        "discount",
        "offer",
        "deal",
        "bundle",

        // Hospitality / lifestyle
        "restaurant",
        "menu",
        "food",
        "drink",
        "hotel",
        "room",
        "reservation",
        "booking",
        "event",
        "venue",
        "travel",
        "tour",
        "holiday",
        "experience",

        // Professional services
        "consulting",
        "advisory",
        "coaching",
        "training",
        "course",
        "program",
        "package",
        "plan",
        "strategy",
        "audit",
        "assessment",

        // Health / wellness
        "clinic",
        "treatment",
        "therapy",
        "session",
        "care",
        "health",
        "fitness",
        "gym",
        "wellness",
        "nutrition",

        // Home / construction
        "construction",
        "repair",
        "maintenance",
        "cleaning",
        "plumbing",
        "electrical",
        "painting",
        "roofing",
        "flooring",
        "landscaping",

        // Marketing / growth
        "campaign",
        "marketing",
        "advertising",
        "seo",
        "branding",
        "design",
        "content",
        "social media",
        "lead generation",
        "conversion",

        // Finance / legal
        "accounting",
        "bookkeeping",
        "tax",
        "insurance",
        "loan",
        "mortgage",
        "investment",
        "legal",
        "compliance",
        "contract",

        // Logistics / operations
        "delivery",
        "shipping",
        "logistics",
        "supply chain",
        "warehouse",
        "fulfillment",
        "transport",
        "fleet"
    ];

    internal static readonly string[] IntentActionTerms =
    [
        "looking for",
        "looking to",
        "need",
        "want",
        "interested in",
        "thinking about",
        "planning",
        "trying to",
        "ready to",

        // Purchase / transaction
        "buy",
        "purchase",
        "order",
        "subscribe",
        "sign up",
        "register",
        "checkout",

        // Booking / engagement
        "book",
        "schedule",
        "reserve",
        "enroll",
        "join",
        "attend",

        // Hiring / services
        "hire",
        "work with",
        "partner with",
        "get help",
        "get support",

        // Pricing / evaluation
        "quote",
        "estimate",
        "pricing",
        "cost",
        "how much",
        "budget",

        // Setup / onboarding
        "start",
        "launch",
        "set up",
        "setup",
        "onboard",
        "deploy",
        "install",
        "configure",

        // Improvement / upgrade
        "upgrade",
        "improve",
        "optimize",
        "scale",
        "migrate",
        "switch",
        "replace",
        "modernize",

        // Fix / troubleshoot
        "fix",
        "repair",
        "troubleshoot",
        "resolve",
        "debug"
    ];

    internal static readonly string[] ExplicitContactTerms =
    [
        "contact",
        "call",
        "phone",
        "callback",
        "call back",
        "reach out",
        "get in touch",
        "talk to someone",
        "speak to someone",
        "speak to a person",
        "speak to an agent",
        "talk to sales",
        "talk to support",
        "contact sales",
        "contact support",
        "email",
        "send a message",
        "message you",
        "live chat",
        "chat with someone"
    ];

    internal static readonly string[] ExplicitQuoteTerms =
    [
        "quote",
        "estimate",
        "pricing",
        "cost",
        "how much",
        "price",
        "rate",
        "fees",
        "charges",
        "budget",
        "quotation",
        "get a quote",
        "request a quote",
        "pricing details"
    ];

    internal static readonly string[] RecommendationPhrases =
    [
        "which one is better",
        "which one should i pick",
        "what do you recommend",
        "which should i choose",
        "which option is best",
        "which plan is best",
        "which package is best",
        "which service is best",
        "which product is best",
        "what is the best option",
        "what is the best choice",
        "what would you suggest",
        "any recommendations",
        "can you recommend",
        "help me choose",
        "i’m not sure which to pick",
        "compare options",
        "difference between",
        "what’s the difference",
        "which is right for me",
        "best for my needs",
        "best for my business",
        "recommend",
        "advice on",
        "guidance on"
    ];
}