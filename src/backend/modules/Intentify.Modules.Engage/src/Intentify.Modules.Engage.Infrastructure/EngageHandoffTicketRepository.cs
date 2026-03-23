using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Engage.Infrastructure;

public sealed class EngageHandoffTicketRepository : IEngageHandoffTicketRepository
{
    private readonly IMongoCollection<EngageHandoffTicket> _tickets;
    private readonly Task _ensureIndexes;

    public EngageHandoffTicketRepository(IMongoDatabase database)
    {
        _tickets = database.GetCollection<EngageHandoffTicket>(EngageMongoCollections.HandoffTickets);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(EngageHandoffTicket ticket, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _tickets.InsertOneAsync(ticket, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var items = await _tickets.Find(item => item.SessionId == sessionId).ToListAsync(cancellationToken);
        return items;
    }

    public async Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var items = await _tickets.Find(item => item.TenantId == tenantId && item.SiteId == siteId).ToListAsync(cancellationToken);
        return items;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<EngageHandoffTicket>(
                Builders<EngageHandoffTicket>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId)),
            new CreateIndexModel<EngageHandoffTicket>(Builders<EngageHandoffTicket>.IndexKeys.Descending(item => item.CreatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_tickets, indexes);
    }
}
