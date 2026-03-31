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

        var recentAssistant = ctx.RecentMessages
            .Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Content?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .TakeLast(2)
            .ToArray();

        if (recentAssistant.Any(prev => IsNearDuplicate(prev, cleaned)))
        {
            if (action is EngageNextAction.AskCaptureQuestion or EngageNextAction.AskDiscoveryQuestion)
            {
                cleaned = $"{cleaned.TrimEnd('.', '!')} — to move this forward, share the one detail I’m missing.";
            }
            else if (action == EngageNextAction.AnswerFactual)
            {
                cleaned = $"{cleaned} If you want, I can tailor this to your exact scope next.";
            }
        }

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

    private static bool IsNearDuplicate(string prior, string current)
    {
        static string Normalize(string input)
            => Regex.Replace(input.ToLowerInvariant(), "[^a-z0-9 ]", " ").Replace("  ", " ").Trim();

        var a = Normalize(prior);
        var b = Normalize(current);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (a == b) return true;
        return a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }
}
