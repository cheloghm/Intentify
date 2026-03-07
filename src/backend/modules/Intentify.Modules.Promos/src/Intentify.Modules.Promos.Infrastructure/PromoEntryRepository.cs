using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Promos.Infrastructure;

public sealed class PromoEntryRepository : IPromoEntryRepository
{
    private readonly IMongoCollection<PromoEntry> _entries;
    private readonly Task _ensureIndexes;
    public PromoEntryRepository(IMongoDatabase database)
    {
        _entries = database.GetCollection<PromoEntry>(PromosMongoCollections.PromoEntries);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(PromoEntry entry, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _entries.InsertOneAsync(entry, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<PromoEntry>> ListByPromoAsync(ListPromoEntriesQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _entries.Find(item => item.TenantId == query.TenantId && item.PromoId == query.PromoId)
            .SortByDescending(item => item.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PromoEntry>> ListByVisitorAsync(ListVisitorPromoEntriesQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _entries.Find(item => item.TenantId == query.TenantId && item.SiteId == query.SiteId && item.VisitorId == query.VisitorId)
            .SortByDescending(item => item.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<PromoEntry>(Builders<PromoEntry>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.PromoId).Descending(item => item.CreatedAtUtc)),
            new CreateIndexModel<PromoEntry>(Builders<PromoEntry>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.SiteId).Ascending(item => item.VisitorId).Descending(item => item.CreatedAtUtc))
        };
        return MongoIndexHelper.EnsureIndexesAsync(_entries, indexes);
    }
}
