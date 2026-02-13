using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class KnowledgeChunkRepository : IKnowledgeChunkRepository
{
    private readonly IMongoCollection<KnowledgeChunk> _chunks;
    private readonly Task _ensureIndexes;

    public KnowledgeChunkRepository(IMongoDatabase database)
    {
        _chunks = database.GetCollection<KnowledgeChunk>(KnowledgeMongoCollections.Chunks);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _chunks.DeleteManyAsync(item => item.TenantId == tenantId && item.SourceId == sourceId, cancellationToken);

        if (chunks.Count > 0)
        {
            await _chunks.InsertManyAsync(chunks, cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _chunks.Find(item => item.TenantId == tenantId && item.SiteId == siteId)
            .ToListAsync(cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<KnowledgeChunk>(Builders<KnowledgeChunk>.IndexKeys
                .Ascending(item => item.TenantId)
                .Ascending(item => item.SiteId)),
            new CreateIndexModel<KnowledgeChunk>(Builders<KnowledgeChunk>.IndexKeys.Ascending(item => item.SourceId)),
            new CreateIndexModel<KnowledgeChunk>(Builders<KnowledgeChunk>.IndexKeys.Ascending(item => item.Content))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_chunks, indexes);
    }
}
