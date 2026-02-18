using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Tickets.Infrastructure;

public sealed class TicketRepository : ITicketRepository
{
    private readonly IMongoCollection<Ticket> _tickets;
    private readonly Task _ensureIndexes;

    public TicketRepository(IMongoDatabase database)
    {
        _tickets = database.GetCollection<Ticket>(TicketsMongoCollections.Tickets);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _tickets.InsertOneAsync(ticket, cancellationToken: cancellationToken);
    }

    public async Task<Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _tickets.Find(ticket => ticket.TenantId == tenantId && ticket.Id == ticketId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<Ticket>.Filter.Eq(ticket => ticket.TenantId, query.TenantId);

        if (query.SiteId is { } siteId)
        {
            filter &= Builders<Ticket>.Filter.Eq(ticket => ticket.SiteId, siteId);
        }

        if (query.VisitorId is { } visitorId)
        {
            filter &= Builders<Ticket>.Filter.Eq(ticket => ticket.VisitorId, visitorId);
        }

        if (query.EngageSessionId is { } sessionId)
        {
            filter &= Builders<Ticket>.Filter.Eq(ticket => ticket.EngageSessionId, sessionId);
        }

        var tickets = await _tickets.Find(filter)
            .SortByDescending(ticket => ticket.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return tickets.Select(ticket => new TicketListItem(
            ticket.Id,
            ticket.SiteId,
            ticket.VisitorId,
            ticket.EngageSessionId,
            ticket.Subject,
            ticket.Status,
            ticket.AssignedToUserId,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc)).ToArray();
    }

    public async Task ReplaceAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _tickets.ReplaceOneAsync(
            existing => existing.TenantId == ticket.TenantId && existing.Id == ticket.Id,
            ticket,
            cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(ticket => ticket.TenantId).Descending(ticket => ticket.CreatedAtUtc)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(ticket => ticket.TenantId).Ascending(ticket => ticket.VisitorId)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(ticket => ticket.TenantId).Ascending(ticket => ticket.EngageSessionId))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_tickets, indexes);
    }
}
