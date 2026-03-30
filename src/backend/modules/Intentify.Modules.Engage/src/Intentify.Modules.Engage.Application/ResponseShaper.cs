using Intentify.Modules.Engage.Domain;
using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class ResponseShaper
{
    public string Shape(string raw, EngageConversationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "How can I help you today?";

        var cleaned = raw.Trim();

        // Remove common filler phrases
        cleaned = Regex.Replace(cleaned, @"(If you want|If you'd like|What would you like to do next\?|Thanks for confirming)", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        // Enforce at most one question and natural flow
        if (!cleaned.EndsWith("?") && !cleaned.Contains("?"))
        {
            var fallback = ctx.LastAssistantQuestion ?? "How can I help you move forward?";
            cleaned += " " + fallback;
        }

        return cleaned;
    }
}
