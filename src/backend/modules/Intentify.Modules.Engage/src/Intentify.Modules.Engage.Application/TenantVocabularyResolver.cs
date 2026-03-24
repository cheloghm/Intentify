using System.Text.RegularExpressions;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Sites.Application;

namespace Intentify.Modules.Engage.Application;

public sealed class TenantVocabularyResolver
{
    private static readonly HashSet<string> StopWords =
    [
        "about", "after", "again", "also", "and", "any", "are", "because", "been", "before", "being", "between",
        "both", "can", "could", "does", "from", "have", "into", "just", "more", "most", "much", "only", "other",
        "our", "over", "same", "should", "some", "than", "that", "their", "there", "these", "they", "this",
        "those", "through", "very", "want", "what", "when", "where", "which", "while", "with", "would", "your"
    ];

    private readonly ISiteRepository _siteRepository;
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;

    public TenantVocabularyResolver(
        ISiteRepository siteRepository,
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository)
    {
        _siteRepository = siteRepository;
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
    }

    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        CancellationToken cancellationToken = default)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var site = await _siteRepository.GetByTenantAndIdAsync(tenantId, siteId, cancellationToken);
        if (site is null)
        {
            return [];
        }

        AddSiteTerms(site.Name, terms);
        AddSiteTerms(site.Domain, terms);
        AddSiteTerms(site.Description, terms);
        AddSiteTerms(site.Category, terms);

        foreach (var tag in site.Tags)
        {
            AddSiteTerms(tag, terms);
        }

        var sources = await _sourceRepository.ListSourcesAsync(tenantId, siteId, cancellationToken);
        var allowedSourceIds = botId.HasValue
            ? sources.Where(item => item.BotId == botId.Value || item.BotId == Guid.Empty).Select(item => item.Id).ToHashSet()
            : sources.Select(item => item.Id).ToHashSet();

        foreach (var source in sources.Where(item => allowedSourceIds.Contains(item.Id)).Take(12))
        {
            AddSiteTerms(source.Name, terms);
            AddSiteTerms(source.Type, terms);
            AddSiteTerms(source.Url, terms);
        }

        var chunks = await _chunkRepository.ListBySiteAsync(tenantId, siteId, cancellationToken);
        foreach (var chunk in chunks.Where(item => allowedSourceIds.Contains(item.SourceId)).Take(80))
        {
            AddChunkTerms(chunk.Content, terms, 8);
            if (terms.Count >= 80)
            {
                break;
            }
        }

        return terms
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToArray();
    }

    private static void AddSiteTerms(string? value, HashSet<string> terms)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var token in Tokenize(value))
        {
            terms.Add(token);
            if (terms.Count >= 80)
            {
                return;
            }
        }
    }

    private static void AddChunkTerms(string? value, HashSet<string> terms, int maxFromChunk)
    {
        if (string.IsNullOrWhiteSpace(value) || maxFromChunk <= 0)
        {
            return;
        }

        var localCount = 0;
        foreach (var token in Tokenize(value))
        {
            if (terms.Add(token))
            {
                localCount++;
            }

            if (localCount >= maxFromChunk || terms.Count >= 80)
            {
                return;
            }
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match match in Regex.Matches(text.ToLowerInvariant(), "[a-z0-9][a-z0-9\\-]{2,}"))
        {
            var value = match.Value.Trim('-');
            if (value.Length < 3 || value.Length > 32)
            {
                continue;
            }

            if (StopWords.Contains(value))
            {
                continue;
            }

            yield return value;
        }
    }
}
