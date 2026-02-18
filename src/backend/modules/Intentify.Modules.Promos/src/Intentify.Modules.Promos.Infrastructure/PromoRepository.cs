using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Promos.Infrastructure;

public sealed class PromoRepository : IPromoRepository
{
    private readonly IMongoCollection<Promo> _promos;
    private readonly Task _ensureIndexes;
    public PromoRepository(IMongoDatabase database)
    {
        _promos = database.GetCollection<Promo>(PromosMongoCollections.Promos);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(Promo promo, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _promos.InsertOneAsync(promo, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<Promo>> ListAsync(ListPromosQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Promo>.Filter.Eq(item => item.TenantId, query.TenantId);
        if (query.SiteId is { } siteId) filter &= Builders<Promo>.Filter.Eq(item => item.SiteId, siteId);
        return await _promos.Find(filter).SortByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<Promo?> GetActiveByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _promos.Find(item => item.PublicKey == publicKey && item.IsActive).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Promo?> GetByIdAsync(Guid tenantId, Guid promoId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _promos.Find(item => item.TenantId == tenantId && item.Id == promoId).FirstOrDefaultAsync(cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Promo>(Builders<Promo>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.SiteId)),
            new CreateIndexModel<Promo>(Builders<Promo>.IndexKeys.Ascending(item => item.PublicKey), new CreateIndexOptions { Unique = true })
        };
        return MongoIndexHelper.EnsureIndexesAsync(_promos, indexes);
    }
}
