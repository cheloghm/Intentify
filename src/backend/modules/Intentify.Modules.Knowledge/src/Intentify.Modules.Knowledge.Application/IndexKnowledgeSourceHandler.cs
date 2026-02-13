using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Knowledge.Application;

public sealed class IndexKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeTextExtractor _extractor;
    private readonly IKnowledgeChunker _chunker;

    public IndexKnowledgeSourceHandler(
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository,
        IKnowledgeTextExtractor extractor,
        IKnowledgeChunker chunker)
    {
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
        _extractor = extractor;
        _chunker = chunker;
    }

    public async Task<OperationResult<IndexKnowledgeSourceResult>> HandleAsync(IndexKnowledgeSourceCommand command, CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(command.TenantId, command.SourceId, cancellationToken);
        if (source is null)
        {
            return OperationResult<IndexKnowledgeSourceResult>.NotFound();
        }

        await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Processing, null, null, cancellationToken);

        var extracted = await _extractor.ExtractAsync(source, cancellationToken);
        if (!extracted.IsSuccess)
        {
            await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Failed, extracted.FailureReason, null, cancellationToken);
            return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Failed.ToString(), 0, extracted.FailureReason));
        }

        var chunks = _chunker.Chunk(extracted.Text ?? string.Empty)
            .Select((content, index) => new KnowledgeChunk
            {
                TenantId = source.TenantId,
                SiteId = source.SiteId,
                SourceId = source.Id,
                ChunkIndex = index,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow
            })
            .ToArray();

        await _chunkRepository.UpsertChunksAsync(command.TenantId, source.Id, chunks, cancellationToken);
        var indexedAt = DateTime.UtcNow;
        await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Indexed, null, indexedAt, cancellationToken);

        return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Indexed.ToString(), chunks.Length, null));
    }
}
