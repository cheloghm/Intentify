using System.Text;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Application;

public sealed class RetrieveTopChunksHandler
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
        var terms = Tokenize(query.Query)
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
                    query.Query,
                    query.TopK,
                    cancellationToken);

                var retrievedFromOpenSearch = openSearchResults
                    .Select(item => new RetrievedChunkResult(
                        item.ChunkId,
                        item.SourceId,
                        item.ChunkIndex,
                        item.Content,
                        Math.Max(1, ScoreChunk(item.Content, terms))))
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
                ScoreChunk(chunk.Content, terms)))
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

    private static int ScoreChunk(string content, IReadOnlyCollection<string> terms)
    {
        var score = 0;
        var lowered = content.ToLowerInvariant();
        foreach (var term in terms)
        {
            var cursor = 0;
            while (cursor < lowered.Length)
            {
                var index = lowered.IndexOf(term, cursor, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                score++;
                cursor = index + term.Length;
            }
        }

        return score;
    }
}
