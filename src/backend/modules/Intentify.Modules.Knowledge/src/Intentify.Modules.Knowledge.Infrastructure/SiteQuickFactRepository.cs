using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class SiteQuickFactRepository : ISiteQuickFactRepository
{
    private readonly IMongoCollection<SiteQuickFact> _collection;
    private readonly Task _ensureIndexes;

    public SiteQuickFactRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SiteQuickFact>(KnowledgeMongoCollections.SiteQuickFacts);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<IReadOnlyCollection<SiteQuickFact>> ListAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _collection
            .Find(f => f.TenantId == tenantId && f.SiteId == siteId)
            .SortByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(SiteQuickFact fact, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _collection.InsertOneAsync(fact, cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid siteId, Guid factId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var result = await _collection.DeleteOneAsync(
            f => f.TenantId == tenantId && f.SiteId == siteId && f.Id == factId,
            cancellationToken);
        return result.DeletedCount > 0;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<SiteQuickFact>(Builders<SiteQuickFact>.IndexKeys
                .Ascending(f => f.TenantId)
                .Ascending(f => f.SiteId)),
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}
