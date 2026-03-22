using System.Text.RegularExpressions;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageInputInterpreter
{
    private static readonly string[] ExplicitNamePrefixes =
    [
        "my name is",
        "i am",
        "i'm",
        "im"
    ];
    private const string ContactDetailsNamePrefix = "my name is";
    private static readonly string[] GreetingTypos =
    [
        "hllo",
        "helo",
        "hy",
        "helllo",
        "helloo",
        "heloo"
    ];
    private static readonly string[] SupportProblemPhrases =
    [
        "not working",
        "isn't working",
        "is not working",
        "doesn't work",
        "doesnt work",
        "broken",
        "failed",
        "image not showing",
        "page not loading",
        "page is blank",
        "blank page",
        "link broken",
        "button not working",
        "contact page isn't working",
        "contact page is not working",
        "contact form not working",
        "form not submitting",
        "cannot submit",
        "directions not clear",
        "information not clear",
        "prices not clear",
        "broken",
        "error",
        "failed",
        "image not showing",
        "page not loading",
        "link broken",
        "directions not clear",
        "information not clear",
        "confusing",
        "unclear",
        "checkout not working",
        "payment failed",
        "refund issue",
        "map not showing",
        "cannot log in",
        "can't log in",
        "cant log in",
        "can't upload",
        "cant upload",
        "code not received"
    ];
    private static readonly string[] SupportProblemTerms =
    [
        "error",
        "issue",
        "problem",
        "failed",
        "broken",
        "confusing",
        "unclear",
        "blank"
    ];
    private static readonly string[] SupportSurfaceTerms =
    [
        "site",
        "website",
        "page",
        "link",
        "button",
        "contact",
        "form",
        "checkout",
        "payment",
        "refund",
        "image",
        "map",
        "directions",
        "information",
        "prices",
        "login",
        "log in",
        "upload",
        "map not showing"
    ];
    private static readonly string[] LocationMarkers =
    [
        " in ",
        " at ",
        " from ",
        " near ",
        " around "
    ];
    private static readonly string[] NonNameContextTerms =
    [
        "office",
        "store",
        "business",
        "location",
        "address",
        "city",
        "country",
        "state",
        "zip",
        "postal",
        "website",
        "service",
        "project",
        "timeline",
        "budget"
    ];

    public string NormalizeUserMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var collapsed = Regex.Replace(message.Trim().ToLowerInvariant(), "[^a-z0-9 ]", " ");
        var normalized = Regex.Replace(collapsed, "\\s+", " ").Trim();

        return normalized
            .Replace("contct", "contact", StringComparison.Ordinal)
            .Replace("cntact", "contact", StringComparison.Ordinal)
            .Replace("dtails", "details", StringComparison.Ordinal)
            .Replace("detals", "details", StringComparison.Ordinal)
            .Replace("servces", "services", StringComparison.Ordinal)
            .Replace("webstie", "website", StringComparison.Ordinal)
            .Replace("websiet", "website", StringComparison.Ordinal)
            .Replace("yur", "your", StringComparison.Ordinal)
            .Replace("recomend", "recommend", StringComparison.Ordinal)
            .Replace("orgnization", "organization", StringComparison.Ordinal)
            .Replace("organisation", "organization", StringComparison.Ordinal)
            .Replace("adress", "address", StringComparison.Ordinal)
            .Replace("locaton", "location", StringComparison.Ordinal);
    }

    public bool IsLikelyGreetingTypo(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        return GreetingTypos.Contains(normalizedMessage, StringComparer.Ordinal);
    }

    public bool ContainsSupportProblemSignal(string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        if (SupportProblemPhrases.Any(phrase => normalizedMessage.Contains(phrase, StringComparison.Ordinal)))
        {
            return true;
        }

        var hasProblemTerm = SupportProblemTerms.Any(term => normalizedMessage.Contains(term, StringComparison.Ordinal));
        if (!hasProblemTerm)
        {
            return false;
        }

        return SupportSurfaceTerms.Any(term => normalizedMessage.Contains(term, StringComparison.Ordinal));
        return SupportProblemPhrases.Any(phrase => normalizedMessage.Contains(phrase, StringComparison.Ordinal));
    }

    public string? TryExtractEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractPhone(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"(?:\+?\d[\d\-\.\(\)\s]{6,}\d)");
        return match.Success ? match.Value.Trim() : null;
    }

    public string? TryExtractName(string message, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var withoutEmail = !string.IsNullOrWhiteSpace(email)
            ? message.Replace(email, string.Empty, StringComparison.OrdinalIgnoreCase)
            : message;

        var withoutContact = !string.IsNullOrWhiteSpace(phone)
            ? withoutEmail.Replace(phone, string.Empty, StringComparison.OrdinalIgnoreCase)
            : withoutEmail;

        var trimmed = withoutContact.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var explicitPrefix = ExplicitNamePrefixes
            .FirstOrDefault(prefix => trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(explicitPrefix))
        {
            var explicitName = trimmed[explicitPrefix.Length..].Trim();
        if (trimmed.StartsWith(ContactDetailsNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var explicitName = trimmed[ContactDetailsNamePrefix.Length..].Trim();
            return CleanNameCandidate(explicitName, allowContextTail: true);
        }

        return CleanNameCandidate(trimmed, allowContextTail: false);
        }
    }

    public bool IsLocationLikeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = $" {text.Trim().ToLowerInvariant()} ";
        return LocationMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal))
            || normalized.Contains("city", StringComparison.Ordinal)
            || normalized.Contains("state", StringComparison.Ordinal)
            || normalized.Contains("country", StringComparison.Ordinal)
            || normalized.Contains("zip", StringComparison.Ordinal)
            || normalized.Contains("postal", StringComparison.Ordinal);
    }

    private string? CleanNameCandidate(string rawCandidate, bool allowContextTail)
    {
        if (string.IsNullOrWhiteSpace(rawCandidate))
        {
            return null;
        }

        var candidate = rawCandidate.Trim();
        if (allowContextTail)
        {
            var markerIndex = IndexOfAny(candidate, [",", ";", " in ", " at ", " from ", " near ", " around "]);
            if (markerIndex > 0)
            {
                candidate = candidate[..markerIndex].Trim();
            }
        }

        candidate = candidate.Trim(' ', ',', '.', ';', ':', '-', '_');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (candidate.Contains('@', StringComparison.Ordinal) || candidate.Any(char.IsDigit))
        {
            return null;
        }

        if (candidate.Contains(',', StringComparison.Ordinal) || IsLocationLikeText(candidate))
        {
            return null;
        }

        var lowered = $" {candidate.ToLowerInvariant()} ";
        if (NonNameContextTerms.Any(term => lowered.Contains($" {term} ", StringComparison.Ordinal)))
        {
            return null;
        }

        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 3)
        {
            return null;
        }

        if (candidate.Length > 40)
        {
            return null;
        }

        return candidate.Length <= 200 ? candidate : candidate[..200];
    }

    private static int IndexOfAny(string input, IReadOnlyCollection<string> markers)
    {
        var positions = markers
            .Select(marker => input.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .ToArray();
        return positions.Length == 0 ? -1 : positions.Min();
    }
}
