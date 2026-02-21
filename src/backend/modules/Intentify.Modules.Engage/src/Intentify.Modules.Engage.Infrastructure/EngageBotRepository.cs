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
            var resolvedName = string.IsNullOrWhiteSpace(existing.Name) ? existing.DisplayName : existing.Name;
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                resolvedName = "Assistant";
            }

            if (!string.Equals(existing.DisplayName, resolvedName, StringComparison.Ordinal)
                || !string.Equals(existing.Name, resolvedName, StringComparison.Ordinal))
            {
                var update = Builders<EngageBot>.Update
                    .Set(item => item.DisplayName, resolvedName)
                    .Set(item => item.Name, resolvedName);
                await _bots.UpdateOneAsync(item => item.Id == existing.Id, update, cancellationToken: cancellationToken);
                existing.DisplayName = resolvedName;
                existing.Name = resolvedName;
            }

            return existing;
        }

        var created = new EngageBot
        {
            TenantId = tenantId,
            SiteId = siteId,
            DisplayName = "Assistant",
            Name = "Assistant"
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

    public async Task<EngageBot?> UpdateNameAsync(Guid tenantId, Guid siteId, string name, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var normalized = name.Trim();
        var update = Builders<EngageBot>.Update
            .Set(item => item.Name, normalized)
            .Set(item => item.DisplayName, normalized);

        return await _bots.FindOneAndUpdateAsync(
            item => item.TenantId == tenantId && item.SiteId == siteId,
            update,
            new FindOneAndUpdateOptions<EngageBot> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
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
