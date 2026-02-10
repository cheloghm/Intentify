namespace Intentify.Shared.Validation;

public static class DomainNormalizer
{
    private const int DefaultMaxLength = 255;

    public static bool TryNormalize(string? domain, out string normalized, int maxLength = DefaultMaxLength)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var trimmed = domain.Trim().ToLowerInvariant();
        if (trimmed.Length > maxLength)
        {
            return false;
        }

        if (trimmed == "localhost")
        {
            normalized = trimmed;
            return true;
        }

        if (trimmed.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        if (!trimmed.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    public static string? Normalize(string? domain, int maxLength = DefaultMaxLength)
    {
        return TryNormalize(domain, out var normalized, maxLength) ? normalized : null;
    }
}
