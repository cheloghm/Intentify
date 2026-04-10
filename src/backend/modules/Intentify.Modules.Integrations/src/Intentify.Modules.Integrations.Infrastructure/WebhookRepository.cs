using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Integrations.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Integrations.Infrastructure;

public sealed class WebhookRepository : IWebhookRepository
{
    private readonly IMongoCollection<WebhookEndpoint> _collection;
    private readonly Task _ensureIndexes;

    public WebhookRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<WebhookEndpoint>("IntegrationsWebhooks");
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<IReadOnlyCollection<WebhookEndpoint>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct = default)
    {
        await _ensureIndexes;
        return await _collection
            .Find(x => x.TenantId == tenantId && x.SiteId == siteId)
            .SortByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<WebhookEndpoint?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await _ensureIndexes;
        return await _collection
            .Find(x => x.TenantId == tenantId && x.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task InsertAsync(WebhookEndpoint webhook, CancellationToken ct = default)
    {
        await _ensureIndexes;
        await _collection.InsertOneAsync(webhook, cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await _ensureIndexes;
        await _collection.DeleteOneAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<WebhookEndpoint>> ListByEventAsync(
        Guid tenantId, Guid siteId, string eventName, CancellationToken ct = default)
    {
        await _ensureIndexes;
        var filter = Builders<WebhookEndpoint>.Filter.Eq(x => x.TenantId, tenantId)
            & Builders<WebhookEndpoint>.Filter.Eq(x => x.SiteId, siteId)
            & Builders<WebhookEndpoint>.Filter.Eq(x => x.IsActive, true)
            & Builders<WebhookEndpoint>.Filter.Regex(x => x.Events, new MongoDB.Bson.BsonRegularExpression(eventName, "i"));
        return await _collection.Find(filter).ToListAsync(ct);
    }

    private async Task EnsureIndexesAsync()
    {
        var ix = _collection.Indexes;
        await ix.CreateOneAsync(
            new CreateIndexModel<WebhookEndpoint>(
                Builders<WebhookEndpoint>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SiteId)),
            cancellationToken: CancellationToken.None);
    }
}
