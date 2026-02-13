namespace Intentify.Modules.Knowledge.Application;

public sealed class RetrieveTopChunksHandler
{
    private readonly IKnowledgeChunkRepository _chunkRepository;

    public RetrieveTopChunksHandler(IKnowledgeChunkRepository chunkRepository)
    {
        _chunkRepository = chunkRepository;
    }

    public async Task<IReadOnlyCollection<RetrievedChunkResult>> HandleAsync(RetrieveTopChunksQuery query, CancellationToken cancellationToken = default)
    {
        var chunks = await _chunkRepository.ListBySiteAsync(query.TenantId, query.SiteId, cancellationToken);
        var terms = query.Query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .Distinct()
            .ToArray();

        return chunks
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
