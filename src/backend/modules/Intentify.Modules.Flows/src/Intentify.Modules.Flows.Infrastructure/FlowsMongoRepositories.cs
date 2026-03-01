using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Flows.Infrastructure;

public sealed class FlowsRepository : IFlowsRepository
{
    private readonly IMongoCollection<FlowDefinition> _collection;
    private readonly Task _ensureIndexes;

    public FlowsRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FlowDefinition>(FlowsMongoCollections.FlowDefinitions);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(FlowDefinition flow, CancellationToken ct = default)
    {
        await _ensureIndexes;
        await _collection.InsertOneAsync(flow, cancellationToken: ct);
    }

    public async Task<FlowDefinition?> GetAsync(Guid tenantId, Guid flowId, CancellationToken ct = default)
    {
        await _ensureIndexes;
        return await _collection.Find(x => x.TenantId == tenantId && x.Id == flowId).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<FlowDefinition>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken ct = default)
    {
        await _ensureIndexes;
        return await _collection.Find(x => x.TenantId == tenantId && x.SiteId == siteId)
            .SortBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<FlowDefinition?> ReplaceAsync(FlowDefinition flow, CancellationToken ct = default)
    {
        await _ensureIndexes;
        var filter = Builders<FlowDefinition>.Filter.Eq(x => x.TenantId, flow.TenantId)
            & Builders<FlowDefinition>.Filter.Eq(x => x.Id, flow.Id);

        return await _collection.FindOneAndReplaceAsync(
            filter,
            flow,
            new FindOneAndReplaceOptions<FlowDefinition> { ReturnDocument = ReturnDocument.After },
            ct);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<FlowDefinition>(
                Builders<FlowDefinition>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SiteId)
                    .Ascending(x => x.Name)),
            new CreateIndexModel<FlowDefinition>(
                Builders<FlowDefinition>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Id),
                new CreateIndexOptions { Unique = true })
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}

public sealed class FlowRunsRepository : IFlowRunsRepository
{
    private readonly IMongoCollection<FlowRun> _collection;
    private readonly Task _ensureIndexes;

    public FlowRunsRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<FlowRun>(FlowsMongoCollections.FlowRuns);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(FlowRun run, CancellationToken ct = default)
    {
        await _ensureIndexes;
        await _collection.InsertOneAsync(run, cancellationToken: ct);
    }

    public async Task<IReadOnlyCollection<FlowRun>> ListByFlowAsync(Guid tenantId, Guid flowId, int limit, CancellationToken ct = default)
    {
        await _ensureIndexes;

        return await _collection.Find(x => x.TenantId == tenantId && x.FlowId == flowId)
            .SortByDescending(x => x.ExecutedAtUtc)
            .Limit(limit)
            .ToListAsync(ct);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<FlowRun>(
                Builders<FlowRun>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.FlowId)
                    .Descending(x => x.ExecutedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_collection, indexes);
    }
}
