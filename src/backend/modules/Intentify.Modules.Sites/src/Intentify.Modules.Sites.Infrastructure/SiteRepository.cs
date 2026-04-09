using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Sites.Infrastructure;

public sealed class SiteRepository : ISiteRepository
{
    private readonly IMongoCollection<Site> _sites;
    private readonly Task _ensureIndexes;

    public SiteRepository(IMongoDatabase database)
    {
        _sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.TenantId == tenantId && site.Domain == domain)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.TenantId == tenantId && site.Id == siteId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Site?> GetByWidgetKeyAsync(string widgetKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.WidgetKey == widgetKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.SiteKey == siteKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Site>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TenantHasSiteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.TenantId == tenantId).AnyAsync(cancellationToken);
    }

    public async Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var count = await _sites.CountDocumentsAsync(site => site.TenantId == tenantId, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task InsertAsync(Site site, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _sites.InsertOneAsync(site, cancellationToken: cancellationToken);
    }

    public async Task<Site?> UpdateAllowedOriginsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<string> allowedOrigins, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteId) &
            Builders<Site>.Filter.Eq(site => site.TenantId, tenantId);
        var update = Builders<Site>.Update
            .Set(site => site.AllowedOrigins, allowedOrigins.ToList())
            .Set(site => site.UpdatedAtUtc, DateTime.UtcNow);

        return await _sites.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Site?> RotateKeysAsync(Guid tenantId, Guid siteId, string siteKey, string widgetKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteId) &
            Builders<Site>.Filter.Eq(site => site.TenantId, tenantId);
        var update = Builders<Site>.Update
            .Set(site => site.SiteKey, siteKey)
            .Set(site => site.WidgetKey, widgetKey)
            .Set(site => site.UpdatedAtUtc, DateTime.UtcNow);

        return await _sites.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Site?> UpdateProfileAsync(
        Guid tenantId, Guid siteId, string? name, string domain,
        string? description, string? category, IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteId) &
            Builders<Site>.Filter.Eq(site => site.TenantId, tenantId);
        var update = Builders<Site>.Update
            .Set(site => site.Name, name)
            .Set(site => site.Domain, domain)
            .Set(site => site.Description, description)
            .Set(site => site.Category, category)
            .Set(site => site.Tags, tags.ToList())
            .Set(site => site.UpdatedAtUtc, DateTime.UtcNow);

        return await _sites.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteId) &
            Builders<Site>.Filter.Eq(site => site.FirstEventReceivedAtUtc, null);
        var update = Builders<Site>.Update
            .Set(site => site.FirstEventReceivedAtUtc, timestampUtc)
            .Set(site => site.UpdatedAtUtc, timestampUtc);

        return await _sites.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var result = await _sites.DeleteOneAsync(
            site => site.TenantId == tenantId && site.Id == siteId,
            cancellationToken);
        return result.DeletedCount > 0;
    }

    // ── Phase 7.2: REST API Key Management ────────────────────────────────────

    public async Task AddApiKeyAsync(Guid tenantId, Guid siteId, SiteApiKey apiKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Site>.Filter.Eq(s => s.Id, siteId) &
                     Builders<Site>.Filter.Eq(s => s.TenantId, tenantId);
        var update = Builders<Site>.Update
            .Push(s => s.ApiKeys, apiKey)
            .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

        await _sites.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task RevokeApiKeyAsync(Guid tenantId, Guid siteId, string keyId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        // Target the specific element in the ApiKeys array by KeyId
        var filter = Builders<Site>.Filter.Eq(s => s.Id, siteId) &
                     Builders<Site>.Filter.Eq(s => s.TenantId, tenantId) &
                     Builders<Site>.Filter.ElemMatch(s => s.ApiKeys, k => k.KeyId == keyId);

        var update = Builders<Site>.Update
            .Set("ApiKeys.$.RevokedAtUtc", revokedAtUtc)
            .Set(s => s.UpdatedAtUtc, DateTime.UtcNow);

        await _sites.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Site>(
                Builders<Site>.IndexKeys
                    .Ascending(site => site.TenantId)
                    .Ascending(site => site.Domain),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Site>(
                Builders<Site>.IndexKeys.Ascending(site => site.SiteKey),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Site>(
                Builders<Site>.IndexKeys.Ascending(site => site.WidgetKey),
                new CreateIndexOptions { Unique = true }),
        };

        return MongoIndexHelper.EnsureIndexesAsync(_sites, indexes);
    }
}
