namespace Intentify.Modules.Engage.Application;

internal static class EngageSupportEscalationSignalBank
{
    internal static readonly string[] HumanHelpPhrases =
    [
        // Form / submission issues
        "contact form",
        "form isn't working",
        "form is not working",
        "form not working",
        "form broken",
        "form error",
        "form failed",
        "can't submit",
        "cannot submit",
        "doesn't submit",
        "wont submit",
        "won't submit",
        "submission failed",
        "submit button not working",
        "nothing happens when i submit",
        "form keeps failing",
        "error submitting form",

        // Technical issues
        "not working",
        "isn't working",
        "doesn't work",
        "broken",
        "error",
        "bug",
        "issue",
        "problem",
        "something is wrong",
        "this is not working",
        "it failed",
        "keeps failing",
        "not loading",
        "page not loading",
        "site not working",
        "website down",
        "app not working",
        "system not working",

        // Access / account issues
        "can't login",
        "cannot login",
        "cant log in",
        "login not working",
        "password not working",
        "reset not working",
        "verification failed",
        "account issue",
        "account problem",
        "locked out",
        "can't access",
        "cannot access",

        // Transaction / order issues
        "payment failed",
        "payment not going through",
        "checkout not working",
        "order failed",
        "order not processed",
        "booking failed",
        "reservation failed",

        // General frustration signals
        "this is frustrating",
        "this is annoying",
        "this is ridiculous",
        "this makes no sense",
        "not helpful",
        "doesn't help",
        "wasting my time"
    ];

    internal static readonly string[] HumanHelpRequestPhrases =
    [
        // Direct help requests
        "help me",
        "need help",
        "i need help",
        "someone help",
        "can you help me",
        "please help",
        "assist me",
        "i need assistance",
        "support me",

        // Request to talk to someone
        "talk to",
        "speak to",
        "talk to someone",
        "speak to someone",
        "talk to a person",
        "speak to a person",
        "talk to a human",
        "speak to a human",

        // Human preference
        "human",
        "real person",
        "actual person",
        "not a bot",
        "no bot",
        "not chatbot",
        "stop bot",
        "are you human",

        // Role-based requests
        "agent",
        "representative",
        "support",
        "customer support",
        "customer service",
        "sales rep",
        "sales team",
        "account manager",
        "manager",
        "admin",
        "technician",
        "engineer",

        // Urgent help signals
        "asap",
        "urgent",
        "immediately",
        "right now",
        "quick help",
        "need urgent help",

        // Contact preference
        "email me",
        "call me",
        "text me",
        "message me",
        "reach me",
        "get in touch"
    ];

    internal static readonly string[] ExplicitEscalationTerms =
    [
        // Direct escalation
        "talk to a human",
        "speak to a human",
        "connect me to a human",
        "transfer me to a human",
        "i want a human",
        "i need a human",
        "human support",
        "real human support",

        // Support escalation
        "contact support",
        "reach support",
        "escalate this",
        "escalate issue",
        "raise a ticket",
        "open a ticket",
        "submit a ticket",
        "log a ticket",
        "report issue",
        "report a problem",

        // Callback / contact intent
        "call me",
        "call back",
        "callback",
        "request a call",
        "schedule a call",
        "book a call",
        "phone me",
        "give me a call",
        "can someone call me",

        // Outreach intent
        "reach out",
        "reach out to me",
        "get in touch",
        "contact me",
        "someone contact me",
        "have someone contact me",
        "email me",
        "send me an email",

        // Frustration-driven escalation
        "this is not helping",
        "i need real help",
        "this bot is useless",
        "this is useless",
        "i want to speak to someone",
        "i need to talk to someone now",

        // Business / sales escalation
        "talk to sales",
        "speak to sales",
        "contact sales",
        "sales call",
        "book a demo",
        "request a demo",
        "schedule a demo",
        "talk to an expert",
        "consult an expert"
    ];
}