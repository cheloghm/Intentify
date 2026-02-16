namespace Intentify.Modules.Knowledge.Application;

public sealed class RetrieveTopChunksHandler
{
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeSourceRepository _sourceRepository;

    public RetrieveTopChunksHandler(IKnowledgeChunkRepository chunkRepository, IKnowledgeSourceRepository sourceRepository)
    {
        _chunkRepository = chunkRepository;
        _sourceRepository = sourceRepository;
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
        var terms = query.Query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .Distinct()
            .ToArray();

        return chunks
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
