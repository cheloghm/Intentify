using Intentify.Modules.Collector.Application;
using Intentify.Modules.Collector.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Collector.Infrastructure;

public sealed class CollectorEventRepository : ICollectorEventRepository
{
    private readonly IMongoCollection<CollectorEvent> _events;
    private readonly Task _ensureIndexes;

    public CollectorEventRepository(IMongoDatabase database)
    {
        _events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(CollectorEvent collectorEvent, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _events.InsertOneAsync(collectorEvent, cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<CollectorEvent>(
                Builders<CollectorEvent>.IndexKeys.Ascending(evt => evt.SiteId)),
            new CreateIndexModel<CollectorEvent>(
                Builders<CollectorEvent>.IndexKeys
                    .Ascending(evt => evt.SiteId)
                    .Ascending(evt => evt.SessionId)
                    .Descending(evt => evt.OccurredAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_events, indexes);
    }
}
