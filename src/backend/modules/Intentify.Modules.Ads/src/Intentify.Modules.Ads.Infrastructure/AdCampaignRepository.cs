using Intentify.Modules.Ads.Application;
using Intentify.Modules.Ads.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Ads.Infrastructure;

public sealed class AdCampaignRepository : IAdCampaignRepository
{
    private readonly IMongoCollection<AdCampaign> _campaigns;
    private readonly Task _ensureIndexes;

    public AdCampaignRepository(IMongoDatabase database)
    {
        _campaigns = database.GetCollection<AdCampaign>(AdsMongoCollections.Campaigns);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(AdCampaign campaign, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _campaigns.InsertOneAsync(campaign, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(AdCampaign campaign, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _campaigns.ReplaceOneAsync(item => item.TenantId == campaign.TenantId && item.Id == campaign.Id, campaign, cancellationToken: cancellationToken);
    }

    public async Task<AdCampaign?> GetByIdAsync(Guid tenantId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _campaigns.Find(item => item.TenantId == tenantId && item.Id == campaignId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdCampaign>> ListAsync(ListAdCampaignsQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<AdCampaign>.Filter.Eq(item => item.TenantId, query.TenantId);
        if (query.SiteId is { } siteId)
        {
            filter &= Builders<AdCampaign>.Filter.Eq(item => item.SiteId, siteId);
        }

        return await _campaigns.Find(filter)
            .SortByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AdCampaign?> ReplacePlacementsAsync(Guid tenantId, Guid campaignId, IReadOnlyCollection<AdPlacement> placements, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<AdCampaign>.Filter.Eq(item => item.TenantId, tenantId)
            & Builders<AdCampaign>.Filter.Eq(item => item.Id, campaignId);
        var update = Builders<AdCampaign>.Update
            .Set(item => item.Placements, placements.ToList())
            .Set(item => item.UpdatedAtUtc, updatedAtUtc);

        return await _campaigns.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<AdCampaign> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<AdCampaign>(Builders<AdCampaign>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.SiteId).Descending(item => item.UpdatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_campaigns, indexes);
    }
}
