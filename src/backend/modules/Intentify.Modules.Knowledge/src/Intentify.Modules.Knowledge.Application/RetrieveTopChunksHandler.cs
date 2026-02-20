using System.Text;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Application;

public sealed class RetrieveTopChunksHandler
{
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly ILogger<RetrieveTopChunksHandler> _logger;

    public RetrieveTopChunksHandler(
        IKnowledgeChunkRepository chunkRepository,
        IKnowledgeSourceRepository sourceRepository,
        ILogger<RetrieveTopChunksHandler> logger)
    {
        _chunkRepository = chunkRepository;
        _sourceRepository = sourceRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<RetrievedChunkResult>> HandleAsync(RetrieveTopChunksQuery query, CancellationToken cancellationToken = default)
    {
        var sources = await _sourceRepository.ListSourcesAsync(query.TenantId, query.SiteId, cancellationToken);
        var allowedSourceIds = query.BotId.HasValue
            ? sources
                .Where(item => item.BotId == query.BotId.Value || item.BotId == Guid.Empty)
                .Select(item => item.Id)
                .ToHashSet()
            : sources.Select(item => item.Id).ToHashSet();

        var chunks = await _chunkRepository.ListBySiteAsync(query.TenantId, query.SiteId, cancellationToken);
        var terms = Tokenize(query.Query)
            .Distinct()
            .ToArray();

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

        _logger.LogInformation(
            "Knowledge retrieval evaluated {ChunkCount} chunks across {AllowedSourceCount} allowed sources and returned {ResultCount} matches for tenant {TenantId}, site {SiteId}, bot {BotId}.",
            chunks.Count,
            allowedSourceIds.Count,
            retrieved.Length,
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
