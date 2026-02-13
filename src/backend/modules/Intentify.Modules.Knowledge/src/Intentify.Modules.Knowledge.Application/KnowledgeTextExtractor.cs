using System.Text.RegularExpressions;
using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeTextExtractor
{
    Task<(bool IsSuccess, string? Text, string? FailureReason)> ExtractAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
}

public sealed partial class KnowledgeTextExtractor : IKnowledgeTextExtractor
{
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

        var withoutTags = HtmlTagRegex().Replace(input, " ");
        return MultiSpaceRegex().Replace(withoutTags, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();
}
