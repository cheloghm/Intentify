using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intentify.Modules.Knowledge.Application;

public sealed class IndexKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeTextExtractor _extractor;
    private readonly IKnowledgeChunker _chunker;
    private readonly IOpenSearchOptions? _openSearchOptions;
    private readonly IOpenSearchKnowledgeClient? _openSearchClient;
    private readonly ILogger<IndexKnowledgeSourceHandler> _logger;

    public IndexKnowledgeSourceHandler(
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository,
        IKnowledgeTextExtractor extractor,
        IKnowledgeChunker chunker,
        IOpenSearchOptions? openSearchOptions = null,
        IOpenSearchKnowledgeClient? openSearchClient = null,
        ILogger<IndexKnowledgeSourceHandler>? logger = null)
    {
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
        _extractor = extractor;
        _chunker = chunker;
        _openSearchOptions = openSearchOptions;
        _openSearchClient = openSearchClient;
        _logger = logger ?? NullLogger<IndexKnowledgeSourceHandler>.Instance;
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

        if (_openSearchOptions?.Enabled == true && _openSearchClient is not null)
        {
            try
            {
                var openSearchDocs = chunks
                    .Select(chunk => new OpenSearchChunkDocument(
                        chunk.SourceId,
                        chunk.Id,
                        chunk.ChunkIndex,
                        chunk.Content,
                        source.BotId))
                    .ToArray();

                await _openSearchClient.EnsureIndexExistsAsync(cancellationToken);
                await _openSearchClient.BulkUpsertChunksAsync(source.TenantId, source.SiteId, source.BotId, openSearchDocs, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "OpenSearch indexing failed for tenant {TenantId}, site {SiteId}, source {SourceId}. {ExceptionType}: {ExceptionMessage}",
                    source.TenantId,
                    source.SiteId,
                    source.Id,
                    exception.GetType().Name,
                    exception.Message);
            }
        }

        var indexedAt = DateTime.UtcNow;
        await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Indexed, null, indexedAt, cancellationToken);

        return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Indexed.ToString(), chunks.Length, null));
    }
}

public interface IOpenSearchOptions
{
    bool Enabled { get; }
}

public interface IOpenSearchKnowledgeClient
{
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default);

    Task BulkUpsertChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        IReadOnlyCollection<OpenSearchChunkDocument> chunkDocs,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OpenSearchChunkDocument>> SearchTopChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}

public sealed record OpenSearchChunkDocument(
    Guid SourceId,
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    Guid BotId);
