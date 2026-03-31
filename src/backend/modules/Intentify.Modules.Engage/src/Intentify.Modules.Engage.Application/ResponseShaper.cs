using Intentify.Modules.Engage.Domain;
using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class ResponseShaper
{
    public string Shape(string raw, EngageConversationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "How can I help you today?";
        }

        var cleaned = raw.Trim();

        // Remove common filler phrases
        cleaned = Regex.Replace(
            cleaned,
            @"(If you want|If you'd like|What would you like to do next\?|Thanks for confirming)",
            "",
            RegexOptions.IgnoreCase);

        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        // Collapse repeated trailing duplicate text blocks
        cleaned = Regex.Replace(cleaned, @"(.+?)(?:\s+\1)+$", "$1", RegexOptions.IgnoreCase).Trim();

        // Enforce at most one question
        var questionCount = cleaned.Count(c => c == '?');
        if (questionCount > 1)
        {
            var firstQuestion = cleaned.IndexOf('?');
            if (firstQuestion >= 0)
            {
                cleaned = cleaned[..(firstQuestion + 1)].Trim();
            }
        }

        var isClosureStyle =
            cleaned.EndsWith(".", StringComparison.Ordinal) &&
            (cleaned.Contains("thank", StringComparison.OrdinalIgnoreCase)
             || cleaned.Contains("reach out", StringComparison.OrdinalIgnoreCase)
             || cleaned.Contains("follow up", StringComparison.OrdinalIgnoreCase));

        if (isClosureStyle)
        {
            return cleaned;
        }

        if (!cleaned.Contains('?'))
        {
            var fallback = ctx.LastAssistantQuestion ?? "How can I help you move forward?";

            if (!cleaned.Contains(fallback, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = $"{cleaned} {fallback}".Trim();
            }
        }

        return cleaned;
    }
}
