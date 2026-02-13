using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Engage.Infrastructure;

public sealed class EngageChatMessageRepository : IEngageChatMessageRepository
{
    private readonly IMongoCollection<EngageChatMessage> _messages;
    private readonly Task _ensureIndexes;

    public EngageChatMessageRepository(IMongoDatabase database)
    {
        _messages = database.GetCollection<EngageChatMessage>(EngageMongoCollections.ChatMessages);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(EngageChatMessage message, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _messages.InsertOneAsync(message, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<EngageChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var results = await _messages.Find(item => item.SessionId == sessionId)
            .SortBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return results;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<EngageChatMessage>(Builders<EngageChatMessage>.IndexKeys.Ascending(item => item.SessionId)),
            new CreateIndexModel<EngageChatMessage>(Builders<EngageChatMessage>.IndexKeys.Ascending(item => item.CreatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_messages, indexes);
    }
}
