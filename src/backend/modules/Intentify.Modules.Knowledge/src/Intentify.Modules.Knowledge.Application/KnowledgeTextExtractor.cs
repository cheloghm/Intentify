using System.Text.RegularExpressions;
using System.Net;
using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeTextExtractor
{
    Task<(bool IsSuccess, string? Text, string? FailureReason)> ExtractAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
}

public sealed partial class KnowledgeTextExtractor : IKnowledgeTextExtractor
{
    private static readonly string[] BoilerplateMarkers =
    [
        "cookie",
        "privacy policy",
        "terms of service",
        "all rights reserved",
        "google analytics",
        "gtag(",
        "subscribe",
        "newsletter",
        "advertisement",
        "powered by"
    ];

    private readonly IHttpClientFactory _httpClientFactory;

    public KnowledgeTextExtractor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(bool IsSuccess, string? Text, string? FailureReason)> ExtractAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        if (source.Type.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            return (true, source.TextContent ?? string.Empty, null);
        }

        if (source.Type.Equals("Pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "PDF extraction not supported");
        }

        if (source.Type.Equals("Url", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(source.Url))
            {
                return (false, null, "URL is missing");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("knowledge");
                var html = await client.GetStringAsync(source.Url, cancellationToken);
                return (true, StripHtml(html), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        return (false, null, "Unsupported source type");
    }

    public static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var withoutComments = HtmlCommentRegex().Replace(input, " ");
        var withoutNoiseBlocks = NoiseBlockRegex().Replace(withoutComments, " ");
        var withHeadingMarkers = HeadingOpenTagRegex().Replace(withoutNoiseBlocks, "\n\n# ");
        var withLineBreaks = BlockBoundaryTagRegex().Replace(withHeadingMarkers, "\n");
        var withoutTags = HtmlTagRegex().Replace(withLineBreaks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalizedLines = decoded
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => MultiSpaceRegex().Replace(line, " ").Trim())
            .Where(line => line.Length > 0)
            .Where(line => !IsLikelyBoilerplateLine(line))
            .ToArray();

        return string.Join("\n", normalizedLines);
    }

    private static bool IsLikelyBoilerplateLine(string line)
    {
        var normalized = line.Trim();
        if (normalized.Length == 0)
        {
            return true;
        }

        var lowered = normalized.ToLowerInvariant();
        if (lowered.Contains("javascript")
            || lowered.Contains("utm_")
            || lowered.Contains("window.")
            || lowered.Contains("document."))
        {
            return true;
        }

        var markerHits = BoilerplateMarkers.Count(lowered.Contains);
        return markerHits >= 2;
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex("<(script|style|noscript|template|svg|canvas|iframe|nav|footer|header|aside)[^>]*>.*?</\\1>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex NoiseBlockRegex();

    [GeneratedRegex("<h[1-6][^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOpenTagRegex();

    [GeneratedRegex("</?(p|div|section|article|main|li|ul|ol|br|hr|table|tr|td|th|h[1-6])[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryTagRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();
}
