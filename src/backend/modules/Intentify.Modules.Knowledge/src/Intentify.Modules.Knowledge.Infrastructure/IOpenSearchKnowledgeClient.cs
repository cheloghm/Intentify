namespace Intentify.Modules.Knowledge.Infrastructure;

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

    Task DeleteBySourceAsync(
        Guid tenantId,
        Guid siteId,
        Guid sourceId,
        Guid? botId,
        CancellationToken cancellationToken = default);
}
