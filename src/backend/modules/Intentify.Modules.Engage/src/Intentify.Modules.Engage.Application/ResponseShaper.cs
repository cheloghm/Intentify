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

        cleaned = Regex.Replace(
            cleaned,
            @"(If you want|If you'd like|Thanks for confirming)",
            "",
            RegexOptions.IgnoreCase);

        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"(.+?)(?:\s+\1)+$", "$1", RegexOptions.IgnoreCase).Trim();

        var action = ctx.PrimaryActionDecision?.Action;
        if (action is EngageNextAction.CloseConversation or EngageNextAction.AnswerFactual)
        {
            return cleaned;
        }

        if (ctx.Session.IsConversationComplete)
        {
            return cleaned;
        }

        if (!cleaned.Contains('?'))
        {
            var likelyPrompt = action is EngageNextAction.AskCaptureQuestion or EngageNextAction.AskDiscoveryQuestion;
            if (likelyPrompt)
            {
                cleaned = $"{cleaned.TrimEnd('.', '!')}?";
            }
        }

        return cleaned;
    }
}
