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

        // Enforce a single, executable prompt contract: one short response with at most one question.
        var questionCount = cleaned.Count(c => c == '?');
        if (questionCount > 1)
        {
            var firstQuestion = cleaned.IndexOf('?');
            cleaned = cleaned[..(firstQuestion + 1)];
        }

        if (!cleaned.EndsWith("?") && !cleaned.Contains("?"))
        {
            var fallback = ctx.LastAssistantQuestion ?? "How can I help you move forward?";
            cleaned += " " + fallback;
        }

        return cleaned;
    }
}
