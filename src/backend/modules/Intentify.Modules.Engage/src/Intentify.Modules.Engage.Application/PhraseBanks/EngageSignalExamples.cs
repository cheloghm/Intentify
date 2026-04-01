using System.Text;

namespace Intentify.Modules.Engage.Application.PhraseBanks;

/// <summary>
/// Provides consolidated signal examples derived from the phrase banks.
/// These are injected into the AI prompt as descriptive context — not decision logic.
/// The AI uses these examples to recognise intent patterns, not to match against lists.
/// </summary>
public static class EngageSignalExamples
{
    public static string BuildSection()
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Signal Examples (for pattern recognition — not decision rules)");
        sb.AppendLine();
        sb.AppendLine("Use these examples to recognise intent patterns in what the visitor writes.");
        sb.AppendLine("These are illustrations, not exhaustive lists. Use your judgement.");
        sb.AppendLine();

        sb.AppendLine("### Greeting patterns");
        sb.AppendLine("Visitors often open with variants of: hi, hello, hey, hiya, good morning, yo, sup,");
        sb.AppendLine("helo, hii, heya, howdy, greetings, or just a single word like \"hi\" or a wave emoji.");
        sb.AppendLine("Short openers with no real question are typically greetings, not requests for help.");
        sb.AppendLine();

        sb.AppendLine("### Support / human escalation signals");
        sb.AppendLine("Phrases that suggest a visitor wants a human or has a problem:");
        sb.AppendLine("  - \"I need to speak to someone\", \"talk to a person\", \"get me a human\",");
        sb.AppendLine("    \"speak to an agent\", \"can I talk to support\", \"connect me to someone\"");
        sb.AppendLine("  - Problem indicators: \"my form isn't working\", \"I can't log in\", \"payment failed\",");
        sb.AppendLine("    \"error on checkout\", \"something's broken\", \"I keep getting an error\",");
        sb.AppendLine("    \"I'm locked out\", \"my account isn't working\"");
        sb.AppendLine("  - Frustration: \"this is ridiculous\", \"I've been waiting\", \"no one is helping me\",");
        sb.AppendLine("    \"I give up\", \"this doesn't work\"");
        sb.AppendLine();

        sb.AppendLine("### Commercial intent signals");
        sb.AppendLine("Phrases that suggest a visitor is interested in services or is ready to engage:");
        sb.AppendLine("  - Action terms: looking for, need, want, interested in, considering, thinking about,");
        sb.AppendLine("    ready to, planning to, want to buy, want to hire, want to get started");
        sb.AppendLine("  - Topic terms: website, app, software, design, marketing, branding, development,");
        sb.AppendLine("    restaurant, retail, consulting, audit, renovation, build, project, service");
        sb.AppendLine("  - Explicit contact requests: \"can someone call me\", \"I'd like a quote\",");
        sb.AppendLine("    \"can I speak to someone\", \"I want to get in touch\", \"send me a proposal\",");
        sb.AppendLine("    \"can you give me a price\", \"what does it cost\", \"how much do you charge\"");
        sb.AppendLine("  - Recommendation intent: \"which one is better\", \"what would you recommend\",");
        sb.AppendLine("    \"what do most people choose\", \"what's the difference between\"");
        sb.AppendLine();

        sb.AppendLine("### Conversation close signals");
        sb.AppendLine("Phrases that suggest the visitor is done or wrapping up:");
        sb.AppendLine("  - \"that's all I needed\", \"thanks, that helps\", \"I've got what I need\",");
        sb.AppendLine("    \"great, thanks\", \"perfect, thank you\", \"that's it for now\",");
        sb.AppendLine("    \"I'll be in touch\", \"talk soon\", \"bye\", \"goodbye\", \"cheers\"");
        sb.AppendLine();

        sb.AppendLine("### Objection signals");
        sb.AppendLine("Phrases suggesting hesitation or a soft no:");
        sb.AppendLine("  - \"too expensive\", \"not in my budget\", \"just browsing\", \"not right now\",");
        sb.AppendLine("    \"maybe later\", \"not interested\", \"I need to think about it\",");
        sb.AppendLine("    \"I'll come back\", \"I'm not ready yet\"");
        sb.AppendLine();

        return sb.ToString();
    }
}
