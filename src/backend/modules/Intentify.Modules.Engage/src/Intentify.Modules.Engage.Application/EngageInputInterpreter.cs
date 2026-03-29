using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageInputInterpreter
{
    public string NormalizeUserMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var collapsed = Regex.Replace(message.Trim().ToLowerInvariant(), "[^a-z0-9 ]", " ");
        var normalized = Regex.Replace(collapsed, "\\s+", " ").Trim();

        return normalized
            .Replace("contct", "contact")
            .Replace("cntact", "contact")
            .Replace("yur", "your")
            .Replace("webstie", "website")
            .Replace("websiet", "website");
    }

    public string? TryExtractEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractPhone(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var match = Regex.Match(message, @"(?:\+?\d[\d\-\.\(\)\s]{6,}\d)");
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractName(string message, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var cleaned = message;
        if (!string.IsNullOrWhiteSpace(email)) cleaned = cleaned.Replace(email, "");
        if (!string.IsNullOrWhiteSpace(phone)) cleaned = cleaned.Replace(phone, "");

        cleaned = cleaned.Trim(' ', ',', '.', ';', ':', '-', '_');
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length > 50) return null;

        return cleaned;
    }

    public string? TryExtractPreferredContactMethod(string message, string? email, string? phone)
    {
        if (!string.IsNullOrWhiteSpace(email)) return "Email";
        if (!string.IsNullOrWhiteSpace(phone)) return "Phone";

        var normalized = " " + NormalizeUserMessage(message) + " ";
        if (normalized.Contains(" email ") || normalized.Contains(" by email ")) return "Email";
        if (normalized.Contains(" phone ") || normalized.Contains(" by phone ") || normalized.Contains(" call me ")) return "Phone";

        return null;
    }

    // Context-aware short reply helper
    public bool TryExtractShortReplySlot(string message, string? lastQuestion, out string slotType, out string value)
    {
        slotType = "";
        value = "";

        if (string.IsNullOrWhiteSpace(message)) return false;

        if (lastQuestion != null && lastQuestion.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            slotType = "capturedName";
            value = TryExtractName(message, null, null) ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }

        if (lastQuestion != null && (lastQuestion.Contains("email or phone", StringComparison.OrdinalIgnoreCase) || lastQuestion.Contains("reach you", StringComparison.OrdinalIgnoreCase)))
        {
            slotType = "capturedPreferredContactMethod";
            value = TryExtractPreferredContactMethod(message, null, null) ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}
