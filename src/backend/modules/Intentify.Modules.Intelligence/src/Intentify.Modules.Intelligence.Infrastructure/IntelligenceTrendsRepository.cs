using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class IntelligenceTrendsRepository : IIntelligenceTrendsRepository
{
    private readonly IMongoCollection<IntelligenceTrendRecord> _collection;
    private readonly Task _ensureIndexes;

    public IntelligenceTrendsRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IntelligenceTrendRecord>(IntelligenceMongoCollections.Trends);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default)
    {
        await _ensureIndexes;

        var filter = Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.TenantId, record.TenantId)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.SiteId, record.SiteId)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.Category, record.Category)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.Location, record.Location)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.TimeWindow, record.TimeWindow);

        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, record, options, ct);
    }

    public async Task<IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
    {
        await _ensureIndexes;

        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return null;
        }

        var filter = Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.TenantId, tenantGuid)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.SiteId, siteId)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.Category, category)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.Location, location)
            & Builders<IntelligenceTrendRecord>.Filter.Eq(x => x.TimeWindow, timeWindow);

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
    {
        var record = await GetAsync(tenantId, siteId, category, location, timeWindow, ct);
        if (record is null)
        {
            return null;
        }

        return new IntelligenceStatusResponse(
            record.Provider,
            record.Category,
            record.Location,
            record.TimeWindow,
            record.RefreshedAtUtc,
            record.Items.Count);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<IntelligenceTrendRecord>(
                Builders<IntelligenceTrendRecord>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SiteId)
                    .Ascending(x => x.Category)
                    .Ascending(x => x.Location)
                    .Ascending(x => x.TimeWindow),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}
