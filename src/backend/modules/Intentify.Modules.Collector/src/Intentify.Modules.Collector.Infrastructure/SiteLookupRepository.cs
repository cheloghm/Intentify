using Intentify.Modules.Collector.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Collector.Infrastructure;

public sealed class SiteLookupRepository : ISiteLookupRepository
{
    private readonly IMongoCollection<Site> _sites;
    private readonly Task _ensureIndexes;

    public SiteLookupRepository(IMongoDatabase database)
    {
        _sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.SiteKey == siteKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Site?> GetBySnippetIdAsync(Guid snippetId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sites.Find(site => site.SnippetId == snippetId)
            .FirstOrDefaultAsync(cancellationToken);
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
            filter,
            update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Site>(
                Builders<Site>.IndexKeys.Ascending(site => site.SiteKey),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_sites, indexes);
    }
}
