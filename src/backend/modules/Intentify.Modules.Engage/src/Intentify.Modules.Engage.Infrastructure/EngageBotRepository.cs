using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Engage.Infrastructure;

public sealed class EngageBotRepository : IEngageBotRepository
{
    private readonly IMongoCollection<EngageBot> _bots;
    private readonly Task _ensureIndexes;

    public EngageBotRepository(IMongoDatabase database)
    {
        _bots = database.GetCollection<EngageBot>(EngageMongoCollections.Bots);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<EngageBot> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var existing = await GetBySiteAsync(tenantId, siteId, cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.DisplayName))
            {
                var displayName = "Assistant";
                var update = Builders<EngageBot>.Update.Set(item => item.DisplayName, displayName);
                await _bots.UpdateOneAsync(item => item.Id == existing.Id, update, cancellationToken: cancellationToken);
                existing.DisplayName = displayName;
            }

            return existing;
        }

        var created = new EngageBot
        {
            TenantId = tenantId,
            SiteId = siteId,
            DisplayName = "Assistant"
        };

        await _bots.InsertOneAsync(created, cancellationToken: cancellationToken);
        return created;
    }

    public async Task<EngageBot?> GetBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _bots.Find(item => item.TenantId == tenantId && item.SiteId == siteId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<EngageBot>(
                Builders<EngageBot>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_bots, indexes);
    }
}
