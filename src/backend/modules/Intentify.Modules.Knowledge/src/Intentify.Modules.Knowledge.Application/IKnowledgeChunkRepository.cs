using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeChunkRepository
{
    Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
