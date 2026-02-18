using Intentify.Modules.Leads.Application;
using Intentify.Modules.Leads.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Leads.Infrastructure;

public sealed class LeadRepository : ILeadRepository
{
    private readonly IMongoCollection<Lead> _leads;
    private readonly Task _ensureIndexes;

    public LeadRepository(IMongoDatabase database)
    {
        _leads = database.GetCollection<Lead>(LeadsMongoCollections.Leads);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<Lead?> GetByEmailAsync(Guid tenantId, Guid siteId, string email, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _leads.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.PrimaryEmail == email).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Lead?> GetByFirstPartyIdAsync(Guid tenantId, Guid siteId, string firstPartyId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _leads.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.FirstPartyId == firstPartyId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Lead?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _leads.Find(item => item.TenantId == tenantId && item.Id == leadId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _leads.InsertOneAsync(lead, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _leads.ReplaceOneAsync(item => item.TenantId == lead.TenantId && item.Id == lead.Id, lead, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<Lead>> ListAsync(ListLeadsQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var filter = Builders<Lead>.Filter.Eq(item => item.TenantId, query.TenantId);
        if (query.SiteId is { } siteId)
        {
            filter &= Builders<Lead>.Filter.Eq(item => item.SiteId, siteId);
        }

        return await _leads.Find(filter)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Lead>(Builders<Lead>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.SiteId).Ascending(item => item.PrimaryEmail)),
            new CreateIndexModel<Lead>(Builders<Lead>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.SiteId).Ascending(item => item.FirstPartyId)),
            new CreateIndexModel<Lead>(Builders<Lead>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.UpdatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_leads, indexes);
    }
}
