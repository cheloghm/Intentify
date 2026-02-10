using Intentify.Modules.Auth.Application;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Infrastructure;

public sealed class TenantRepository : ITenantRepository
{
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly Task _ensureIndexes;

    public TenantRepository(IMongoDatabase database)
    {
        _tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<Tenant?> GetFirstAsync(CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _tenants.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _tenants.InsertOneAsync(tenant, cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Tenant>(Builders<Tenant>.IndexKeys.Ascending(tenant => tenant.Domain))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_tenants, indexes);
    }
}
