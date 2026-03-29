namespace Intentify.Modules.Engage.Application;

internal static class EngageConversationPolicySignalBank
{
    internal static readonly string[] AmbiguousPromptTerms = ["help", "info", "details", "price"];

    internal static readonly string[] HumanTargetTerms = ["human", "agent", "representative", "person"];
    internal static readonly string[] HandoffVerbTerms = ["need", "want", "speak", "talk", "connect", "help"];

    internal static readonly string[] ContactIntentTerms = ["contact", "phone", "email", "call"];
    internal static readonly string[] LocationIntentTerms = ["location", "address", "where", "located"];
    internal static readonly string[] HoursIntentTerms = ["hours", "open", "close", "time"];
    internal static readonly string[] ServicesIntentTerms = ["service", "menu", "offer", "pricing", "order"];
    internal static readonly string[] OrganizationIntentTerms = ["org", "organization", "business name", "company name", "name of"];

    internal static readonly string[] DigitalContextTerms = ["website", "site", "online store", "ecommerce", "e-commerce"];
    internal static readonly string[] BookingContextTerms = ["booking", "appointment", "reservation", "schedule"];
    internal static readonly string[] CommerceContextTerms = ["retail", "restaurant", "food", "drink", "menu", "inventory", "order"];

    internal static readonly string[] ProjectIntentContextTerms = ["website", "site", "seo", "quote", "project", "redesign", "build", "booking"];

    internal static bool ContainsAny(string message, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => message.Contains(term, StringComparison.Ordinal));
    }
}
