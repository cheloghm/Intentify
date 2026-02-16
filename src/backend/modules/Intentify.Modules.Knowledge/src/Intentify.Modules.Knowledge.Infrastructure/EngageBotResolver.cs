using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Knowledge.Application;
using MongoDB.Driver;

namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class EngageBotResolver : IEngageBotResolver
{
    private readonly IMongoCollection<EngageBot> _bots;

    public EngageBotResolver(IMongoDatabase database)
    {
        _bots = database.GetCollection<EngageBot>(EngageMongoCollections.Bots);
    }

    public async Task<Guid> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var existing = await _bots.Find(item => item.TenantId == tenantId && item.SiteId == siteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing.BotId;
        }

        var created = new EngageBot
        {
            TenantId = tenantId,
            SiteId = siteId
        };

        await _bots.InsertOneAsync(created, cancellationToken: cancellationToken);
        return created.BotId;
    }
}
