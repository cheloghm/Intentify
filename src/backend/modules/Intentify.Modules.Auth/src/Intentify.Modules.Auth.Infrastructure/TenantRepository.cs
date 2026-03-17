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

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _tenants.Find(tenant => tenant.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _tenants.Find(tenant => tenant.Domain == domain).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _tenants.InsertOneAsync(tenant, cancellationToken: cancellationToken);
    }

    public async Task UpdateNameAsync(Guid id, string name, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<Tenant>.Filter.Eq(tenant => tenant.Id, id);
        var update = Builders<Tenant>.Update
            .Set(tenant => tenant.Name, name)
            .Set(tenant => tenant.UpdatedAt, updatedAt);

        await _tenants.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
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
