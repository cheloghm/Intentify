using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class IntelligenceProfileRepository : IIntelligenceProfileRepository
{
    private readonly IMongoCollection<IntelligenceProfile> _collection;
    private readonly Task _ensureIndexes;

    public IntelligenceProfileRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IntelligenceProfile>(IntelligenceMongoCollections.Profiles);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task UpsertAsync(IntelligenceProfile profile, CancellationToken ct = default)
    {
        await _ensureIndexes;

        var filter = Builders<IntelligenceProfile>.Filter.Eq(x => x.TenantId, profile.TenantId)
            & Builders<IntelligenceProfile>.Filter.Eq(x => x.SiteId, profile.SiteId);

        await _collection.ReplaceOneAsync(filter, profile, new ReplaceOptions { IsUpsert = true }, ct);
    }


    public async Task<IReadOnlyList<IntelligenceProfile>> ListActiveAsync(CancellationToken ct = default)
    {
        await _ensureIndexes;

        var filter = Builders<IntelligenceProfile>.Filter.Eq(x => x.IsActive, true);
        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IntelligenceProfile?> GetAsync(string tenantId, Guid siteId, CancellationToken ct = default)
    {
        await _ensureIndexes;

        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return null;
        }

        var filter = Builders<IntelligenceProfile>.Filter.Eq(x => x.TenantId, tenantGuid)
            & Builders<IntelligenceProfile>.Filter.Eq(x => x.SiteId, siteId);

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<IntelligenceProfile>(
                Builders<IntelligenceProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SiteId),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}
