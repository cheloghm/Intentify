using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Tickets.Infrastructure;

public sealed class TicketNoteRepository : ITicketNoteRepository
{
    private readonly IMongoCollection<TicketNote> _notes;
    private readonly Task _ensureIndexes;

    public TicketNoteRepository(IMongoDatabase database)
    {
        _notes = database.GetCollection<TicketNote>(TicketsMongoCollections.TicketNotes);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(TicketNote note, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _notes.InsertOneAsync(note, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<TicketNote>> ListAsync(ListTicketNotesQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var notes = await _notes.Find(note => note.TenantId == query.TenantId && note.TicketId == query.TicketId)
            .SortByDescending(note => note.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return notes;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<TicketNote>(Builders<TicketNote>.IndexKeys.Ascending(note => note.TenantId).Ascending(note => note.TicketId).Descending(note => note.CreatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_notes, indexes);
    }
}
