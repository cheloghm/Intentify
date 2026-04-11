using Intentify.Modules.LinkHub.Application;
using Intentify.Modules.LinkHub.Domain;
using MongoDB.Driver;

namespace Intentify.Modules.LinkHub.Infrastructure;

public sealed class LinkHubRepository : ILinkHubRepository
{
    private readonly IMongoCollection<LinkHubProfile> _profiles;
    private readonly IMongoCollection<LinkHubClick>   _clicks;

    public LinkHubRepository(IMongoDatabase database)
    {
        _profiles = database.GetCollection<LinkHubProfile>(LinkHubMongoCollections.Profiles);
        _clicks   = database.GetCollection<LinkHubClick>(LinkHubMongoCollections.Clicks);

        // Indexes
        var slugIndex = new CreateIndexModel<LinkHubProfile>(
            Builders<LinkHubProfile>.IndexKeys.Ascending(p => p.Slug),
            new CreateIndexOptions { Unique = true, Background = true });
        var tenantIndex = new CreateIndexModel<LinkHubProfile>(
            Builders<LinkHubProfile>.IndexKeys.Ascending(p => p.TenantId),
            new CreateIndexOptions { Unique = true, Background = true });
        _profiles.Indexes.CreateMany([slugIndex, tenantIndex]);

        var clickIndex = new CreateIndexModel<LinkHubClick>(
            Builders<LinkHubClick>.IndexKeys
                .Ascending(c => c.ProfileId)
                .Ascending(c => c.ClickedAtUtc),
            new CreateIndexOptions { Background = true });
        _clicks.Indexes.CreateOne(clickIndex);
    }

    public Task<LinkHubProfile?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _profiles.Find(p => p.TenantId == tenantId).FirstOrDefaultAsync(ct)!;

    public Task<LinkHubProfile?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _profiles.Find(p => p.Slug == slug).FirstOrDefaultAsync(ct)!;

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken ct = default)
    {
        var filter = Builders<LinkHubProfile>.Filter.Eq(p => p.Slug, slug);
        if (excludeTenantId.HasValue)
            filter = Builders<LinkHubProfile>.Filter.And(filter,
                Builders<LinkHubProfile>.Filter.Ne(p => p.TenantId, excludeTenantId.Value));
        return await _profiles.Find(filter).AnyAsync(ct);
    }

    public async Task UpsertAsync(LinkHubProfile profile, CancellationToken ct = default)
    {
        await _profiles.ReplaceOneAsync(
            p => p.TenantId == profile.TenantId,
            profile,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public Task RecordClickAsync(LinkHubClick click, CancellationToken ct = default) =>
        _clicks.InsertOneAsync(click, null, ct);

    public Task IncrementLinkClickAsync(Guid profileId, string linkId, CancellationToken ct = default) =>
        _profiles.UpdateOneAsync(
            Builders<LinkHubProfile>.Filter.And(
                Builders<LinkHubProfile>.Filter.Eq(p => p.Id, profileId),
                Builders<LinkHubProfile>.Filter.ElemMatch(p => p.Links, l => l.Id == linkId)),
            Builders<LinkHubProfile>.Update.Inc("Links.$.ClickCount", 1),
            cancellationToken: ct);

    public async Task<IReadOnlyList<LinkHubClick>> GetClicksAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var filter = Builders<LinkHubClick>.Filter.And(
            Builders<LinkHubClick>.Filter.Eq(c => c.TenantId, tenantId),
            Builders<LinkHubClick>.Filter.Gte(c => c.ClickedAtUtc, from),
            Builders<LinkHubClick>.Filter.Lte(c => c.ClickedAtUtc, to));
        return await _clicks.Find(filter).ToListAsync(ct);
    }
}
