using Intentify.Modules.Engage.Domain;

public sealed class ResponseShaper
{
    public string Shape(string raw, EngageConversationContext ctx)
    {
        var cleaned = raw.Trim();
        // Remove filler, enforce one question max, make tone natural using ctx
        cleaned = Regex.Replace(cleaned, @"(If you want|If you'd like|What would you like to do next\?)", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        if (!cleaned.EndsWith("?") && !cleaned.Contains("?"))
            cleaned += " " + ctx.LastAssistantQuestion ?? "How can I help you move forward?";

        return cleaned;
    }
}
