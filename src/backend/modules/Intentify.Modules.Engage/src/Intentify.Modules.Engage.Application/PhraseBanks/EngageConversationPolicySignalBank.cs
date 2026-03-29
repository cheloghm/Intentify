namespace Intentify.Modules.Engage.Application;

internal static class EngageConversationPolicySignalBank
{
    internal static readonly string[] AmbiguousPromptTerms =
    [
        "help",
        "info",
        "information",
        "details",
        "detail",
        "price",
        "pricing",
        "cost",
        "how much",
        "tell me more",
        "more info",
        "more details",
        "explain",
        "what is this",
        "what do you do",
        "what do you offer",
        "can you help",
        "need help",
        "support",
        "assistance",
        "options",
        "available",
        "services",
        "products"
    ];

    internal static readonly string[] HumanTargetTerms =
    [
        "human",
        "agent",
        "representative",
        "person",
        "real person",
        "someone",
        "support agent",
        "sales rep",
        "advisor",
        "consultant",
        "specialist",
        "technician",
        "engineer",
        "manager",
        "admin",
        "staff",
        "team member"
    ];

    internal static readonly string[] HandoffVerbTerms =
    [
        "need",
        "want",
        "would like",
        "like to",
        "speak",
        "talk",
        "connect",
        "reach",
        "contact",
        "get",
        "have",
        "request",
        "ask for",
        "be connected",
        "be transferred",
        "be put through",
        "be referred",
        "get in touch",
        "reach out",
        "follow up"
    ];

    internal static readonly string[] ContactIntentTerms =
    [
        "contact",
        "phone",
        "email",
        "call",
        "callback",
        "call back",
        "reach out",
        "get in touch",
        "message",
        "text",
        "whatsapp",
        "chat",
        "live chat",
        "send a message",
        "send email",
        "email address",
        "phone number",
        "contact details"
    ];

    internal static readonly string[] LocationIntentTerms =
    [
        "location",
        "address",
        "where",
        "where are you",
        "where is",
        "located",
        "based",
        "find you",
        "directions",
        "map",
        "near",
        "nearby",
        "closest",
        "distance",
        "area",
        "region",
        "city",
        "postcode",
        "zip",
        "how to get there"
    ];

    internal static readonly string[] HoursIntentTerms =
    [
        "hours",
        "open",
        "close",
        "closing",
        "opening",
        "time",
        "working hours",
        "business hours",
        "when are you open",
        "when do you close",
        "availability",
        "available",
        "schedule",
        "operating hours",
        "service hours"
    ];

    internal static readonly string[] ServicesIntentTerms =
    [
        "service",
        "services",
        "menu",
        "offer",
        "offering",
        "products",
        "solutions",
        "packages",
        "plans",
        "pricing",
        "price",
        "cost",
        "order",
        "buy",
        "purchase",
        "book",
        "schedule",
        "subscribe",
        "sign up",
        "features",
        "capabilities",
        "what do you provide",
        "what do you sell",
        "what do you offer"
    ];

    internal static readonly string[] OrganizationIntentTerms =
    [
        "org",
        "organization",
        "organisation",
        "business name",
        "company name",
        "name of",
        "who are you",
        "about you",
        "about company",
        "about business",
        "company info",
        "business info",
        "brand",
        "brand name",
        "team",
        "about us",
        "who is behind",
        "owner",
        "founder"
    ];

    internal static readonly string[] DigitalContextTerms =
    [
        "website",
        "site",
        "web app",
        "app",
        "platform",
        "system",
        "dashboard",
        "portal",
        "online store",
        "ecommerce",
        "e-commerce",
        "saas",
        "software",
        "application",
        "tool",
        "interface",
        "frontend",
        "backend",
        "api"
    ];

    internal static readonly string[] BookingContextTerms =
    [
        "booking",
        "appointment",
        "reservation",
        "schedule",
        "book",
        "reserve",
        "slot",
        "availability",
        "calendar",
        "time slot",
        "meeting",
        "consultation",
        "session",
        "visit",
        "demo",
        "trial"
    ];

    internal static readonly string[] CommerceContextTerms =
    [
        "retail",
        "store",
        "shop",
        "restaurant",
        "food",
        "drink",
        "menu",
        "inventory",
        "product",
        "products",
        "catalog",
        "checkout",
        "cart",
        "order",
        "delivery",
        "shipping",
        "payment",
        "refund",
        "discount",
        "offer",
        "deal",
        "sale"
    ];

    internal static readonly string[] ProjectIntentContextTerms =
    [
        "website",
        "site",
        "seo",
        "quote",
        "estimate",
        "project",
        "redesign",
        "build",
        "develop",
        "development",
        "design",
        "create",
        "launch",
        "setup",
        "set up",
        "booking system",
        "ecommerce",
        "integration",
        "automation",
        "migration",
        "upgrade",
        "improve",
        "optimize"
    ];

    internal static bool ContainsAny(string message, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => message.Contains(term, StringComparison.Ordinal));
    }
}