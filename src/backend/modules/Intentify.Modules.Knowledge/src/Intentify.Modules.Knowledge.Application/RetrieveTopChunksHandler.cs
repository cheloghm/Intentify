using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Application;

public sealed partial class RetrieveTopChunksHandler
{
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IOpenSearchOptions? _openSearchOptions;
    private readonly IOpenSearchKnowledgeClient? _openSearchClient;
    private readonly ILogger<RetrieveTopChunksHandler> _logger;

    public RetrieveTopChunksHandler(
        IKnowledgeChunkRepository chunkRepository,
        IKnowledgeSourceRepository sourceRepository,
        ILogger<RetrieveTopChunksHandler> logger,
        IOpenSearchOptions? openSearchOptions = null,
        IOpenSearchKnowledgeClient? openSearchClient = null)
    {
        _chunkRepository = chunkRepository;
        _sourceRepository = sourceRepository;
        _logger = logger;
        _openSearchOptions = openSearchOptions;
        _openSearchClient = openSearchClient;
    }

    public async Task<IReadOnlyCollection<RetrievedChunkResult>> HandleAsync(RetrieveTopChunksQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeSearchText(query.Query);
        var terms = Tokenize(normalizedQuery)
            .SelectMany(ExpandTermVariants)
            .Where(IsInformativeTerm)
            .Distinct()
            .ToArray();

        if (_openSearchOptions?.Enabled == true && _openSearchClient is not null)
        {
            try
            {
                var openSearchResults = await _openSearchClient.SearchTopChunksAsync(
                    query.TenantId,
                    query.SiteId,
                    query.BotId,
                    normalizedQuery,
                    query.TopK,
                    cancellationToken);

                var retrievedFromOpenSearch = openSearchResults
                    .Select(item => new RetrievedChunkResult(
                        item.ChunkId,
                        item.SourceId,
                        item.ChunkIndex,
                        item.Content,
                        Math.Max(1, ScoreChunk(item.Content, terms, normalizedQuery))))
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.ChunkIndex)
                    .Take(query.TopK)
                    .ToArray();

                var topScore = retrievedFromOpenSearch.Length == 0 ? 0 : retrievedFromOpenSearch.Max(item => item.Score);
                _logger.LogInformation(
                    "Knowledge retrieval (OpenSearch) returned {ReturnedCount} matches with top score {TopScore} for tenant {TenantId}, site {SiteId}, bot {BotId}.",
                    retrievedFromOpenSearch.Length,
                    topScore,
                    query.TenantId,
                    query.SiteId,
                    query.BotId);

                return retrievedFromOpenSearch;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Knowledge retrieval (OpenSearch) failed for tenant {TenantId}, site {SiteId}, bot {BotId}. Falling back to Mongo retrieval.",
                    query.TenantId,
                    query.SiteId,
                    query.BotId);
            }
        }

        var sources = await _sourceRepository.ListSourcesAsync(query.TenantId, query.SiteId, cancellationToken);
        var allowedSourceIds = query.BotId.HasValue
            ? sources
                .Where(item => item.BotId == query.BotId.Value || item.BotId == Guid.Empty)
                .Select(item => item.Id)
                .ToHashSet()
            : sources.Select(item => item.Id).ToHashSet();

        var chunks = await _chunkRepository.ListBySiteAsync(query.TenantId, query.SiteId, cancellationToken);

        var retrieved = chunks
            .Where(chunk => allowedSourceIds.Contains(chunk.SourceId))
            .Select(chunk => new RetrievedChunkResult(
                chunk.Id,
                chunk.SourceId,
                chunk.ChunkIndex,
                chunk.Content,
                ScoreChunk(chunk.Content, terms, normalizedQuery)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.ChunkIndex)
            .Take(query.TopK)
            .ToArray();

        var mongoTopScore = retrieved.Length == 0 ? 0 : retrieved.Max(item => item.Score);
        _logger.LogInformation(
            "Knowledge retrieval (Mongo fallback) evaluated {ChunkCount} chunks across {AllowedSourceCount} allowed sources and returned {ReturnedCount} matches with top score {TopScore} for tenant {TenantId}, site {SiteId}, bot {BotId}.",
            chunks.Count,
            allowedSourceIds.Count,
            retrieved.Length,
            mongoTopScore,
            query.TenantId,
            query.SiteId,
            query.BotId);

        return retrieved;
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        var buffer = new StringBuilder();
        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (buffer.Length <= 0)
            {
                continue;
            }

            yield return buffer.ToString();
            buffer.Clear();
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }

    private static int ScoreChunk(string content, IReadOnlyCollection<string> terms, string normalizedQuery)
    {
        if (terms.Count == 0 || string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        var score = 0;
        var normalizedContent = NormalizeSearchText(content);
        var matchedTerms = 0;

        foreach (var term in terms)
        {
            var wholeMatches = CountWholeWordMatches(normalizedContent, term);
            if (wholeMatches > 0)
            {
                score += wholeMatches * 4;
                matchedTerms++;
                continue;
            }

            var partialMatches = CountSubstringMatches(normalizedContent, term);
            if (partialMatches > 0)
            {
                score += partialMatches;
                matchedTerms++;
                continue;
            }

        }

        if (matchedTerms > 1)
        {
            score += matchedTerms * 2;
        }

        if (normalizedQuery.Length > 4 && normalizedContent.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 6;
        }

        if (LooksLikeHeadingHit(content, terms))
        {
            score += 3;
        }

        return score;
    }

    private static string NormalizeSearchText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(' ');
            }
        }

        return MultiWhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static IEnumerable<string> ExpandTermVariants(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            yield break;
        }

        yield return term;

        if (term.Length > 4 && term.EndsWith("es", StringComparison.Ordinal))
        {
            yield return term[..^2];
        }
        else if (term.Length > 3 && term.EndsWith('s'))
        {
            yield return term[..^1];
        }

        var deduped = DeduplicateRepeatedLetters(term);
        if (!string.Equals(deduped, term, StringComparison.Ordinal) && deduped.Length > 2)
        {
            yield return deduped;
        }
    }

    private static bool IsInformativeTerm(string term)
    {
        if (term.Length <= 1)
        {
            return false;
        }

        return term is not "the"
            and not "a"
            and not "an"
            and not "is"
            and not "are"
            and not "what"
            and not "how"
            and not "when"
            and not "where"
            and not "why"
            and not "can";
    }

    private static int CountWholeWordMatches(string content, string term)
    {
        var count = 0;
        var cursor = 0;

        while (cursor < content.Length)
        {
            var index = content.IndexOf(term, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            var startsAtBoundary = index == 0 || content[index - 1] == ' ';
            var endIndex = index + term.Length;
            var endsAtBoundary = endIndex >= content.Length || content[endIndex] == ' ';

            if (startsAtBoundary && endsAtBoundary)
            {
                count++;
            }

            cursor = endIndex;
        }

        return count;
    }

    private static int CountSubstringMatches(string content, string term)
    {
        var count = 0;
        var cursor = 0;

        while (cursor < content.Length)
        {
            var index = content.IndexOf(term, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            cursor = index + term.Length;
        }

        return count;
    }

    private static bool LooksLikeHeadingHit(string content, IReadOnlyCollection<string> terms)
    {
        var lowered = content.ToLowerInvariant();
        return terms.Any(term =>
            lowered.Contains($"# {term}", StringComparison.Ordinal)
            || lowered.Contains($"\n{term}:", StringComparison.Ordinal));
    }

    private static string DeduplicateRepeatedLetters(string term)
    {
        var builder = new StringBuilder(term.Length);
        var previous = '\0';

        foreach (var character in term)
        {
            if (character == previous)
            {
                continue;
            }

            builder.Append(character);
            previous = character;
        }

        return builder.ToString();
    }

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex MultiWhitespaceRegex();
}
