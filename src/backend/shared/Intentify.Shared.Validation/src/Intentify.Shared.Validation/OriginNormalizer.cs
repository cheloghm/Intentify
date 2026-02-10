namespace Intentify.Shared.Validation;

public static class OriginNormalizer
{
    public static bool TryNormalize(string? origin, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var trimmed = origin.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        if (uri.PathAndQuery is not ("" or "/"))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    public static string? Normalize(string? origin)
    {
        return TryNormalize(origin, out var normalized) ? normalized : null;
    }
}
