namespace Intentify.Modules.Engage.Application;

internal static class EngageInputExtractionSignalBank
{
    internal static readonly string[] ExplicitNamePrefixes =
    [
        // Direct introductions
        "my name is",
        "i am",
        "i'm",
        "im",
        "this is",
        "it is",
        "name is",

        // Polite / conversational
        "hi i'm",
        "hello i'm",
        "hey i'm",
        "hi i am",
        "hello i am",
        "hey i am",

        // Informal / shorthand
        "its",
        "it's",
        "me",
        "this is me",
        "call me",
        "you can call me",

        // Email-style / form-like
        "name:",
        "full name is",
        "first name is",
        "last name is",

        // Variations with typos
        "my name's",
        "i am called",
        "i'm called",
        "im called"
    ];

    internal static readonly string[] LocationMarkers =
    [
        // Core
        " in ",
        " at ",
        " from ",
        " near ",
        " around ",

        // Expanded location indicators
        " based in ",
        " located in ",
        " located at ",
        " living in ",
        " live in ",
        " staying in ",
        " working in ",
        " operating in ",
        " serving in ",
        " covering ",
        " within ",
        " close to ",
        " not far from ",
        " just outside ",
        " just near ",
        " nearby ",
        " around the area of ",
        " in the area of ",
        " out of ",
        " across ",
        " throughout ",
        " inside ",
        " outside ",
        " offshore ",
        " onsite in "
    ];

    internal static readonly string[] NonNameContextTerms =
    [
        // Business / place context
        "office",
        "store",
        "shop",
        "business",
        "company",
        "organization",
        "organisation",
        "firm",
        "agency",
        "branch",
        "headquarters",
        "hq",

        // Address / location context
        "location",
        "address",
        "city",
        "country",
        "state",
        "region",
        "area",
        "district",
        "zip",
        "zipcode",
        "postal",
        "postcode",
        "pin",
        "coordinates",

        // Contact / web context
        "website",
        "url",
        "link",
        "email",
        "phone",
        "contact",
        "mobile",
        "telephone",

        // Service / business intent context
        "service",
        "services",
        "project",
        "task",
        "job",
        "requirement",
        "request",
        "inquiry",
        "enquiry",
        "timeline",
        "deadline",
        "budget",
        "cost",
        "pricing",
        "quote",
        "estimate",

        // Technical / digital context
        "app",
        "application",
        "platform",
        "system",
        "software",
        "tool",
        "integration",
        "api",
        "dashboard",
        "data",

        // Commerce / transaction context
        "order",
        "booking",
        "appointment",
        "reservation",
        "purchase",
        "subscription",
        "checkout",

        // Descriptive / non-person entities
        "team",
        "department",
        "support",
        "sales",
        "admin",
        "manager",
        "staff",
        "user",
        "client",
        "customer",

        // Misc noise to avoid false name extraction
        "example",
        "test",
        "demo",
        "sample",
        "info",
        "details",
        "information",
        "something",
        "anything",
        "everything"
    ];
}