using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class KnowledgeQuickFactsRepository : IKnowledgeQuickFactsRepository
{
    private readonly IMongoCollection<KnowledgeQuickFacts> _collection;
    private readonly Task _ensureIndexes;

    public KnowledgeQuickFactsRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<KnowledgeQuickFacts>(KnowledgeMongoCollections.QuickFacts);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task UpsertAsync(KnowledgeQuickFacts quickFacts, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<KnowledgeQuickFacts>.Filter.And(
            Builders<KnowledgeQuickFacts>.Filter.Eq(x => x.TenantId, quickFacts.TenantId),
            Builders<KnowledgeQuickFacts>.Filter.Eq(x => x.SourceId, quickFacts.SourceId));

        await _collection.ReplaceOneAsync(filter, quickFacts, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<KnowledgeQuickFacts?> GetBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _collection
            .Find(x => x.TenantId == tenantId && x.SourceId == sourceId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<KnowledgeQuickFacts>> GetBySourceIdsAsync(
        Guid tenantId,
        Guid siteId,
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        if (sourceIds.Count == 0)
            return [];

        return await _collection
            .Find(x => x.TenantId == tenantId && x.SiteId == siteId && sourceIds.Contains(x.SourceId))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _collection.DeleteManyAsync(x => x.TenantId == tenantId && x.SourceId == sourceId, cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<KnowledgeQuickFacts>(Builders<KnowledgeQuickFacts>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.SiteId)),
            new CreateIndexModel<KnowledgeQuickFacts>(Builders<KnowledgeQuickFacts>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.SourceId),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}
