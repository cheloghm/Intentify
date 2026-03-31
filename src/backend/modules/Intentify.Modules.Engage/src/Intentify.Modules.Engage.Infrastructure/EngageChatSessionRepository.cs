using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Engage.Infrastructure;

public sealed class EngageChatSessionRepository : IEngageChatSessionRepository
{
    private readonly IMongoCollection<EngageChatSession> _sessions;
    private readonly Task _ensureIndexes;

    public EngageChatSessionRepository(IMongoDatabase database)
    {
        _sessions = database.GetCollection<EngageChatSession>(EngageMongoCollections.ChatSessions);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<EngageChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sessions.Find(item => item.Id == sessionId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EngageChatSession?> GetByIdAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sessions.Find(item => item.Id == sessionId && item.TenantId == tenantId && item.SiteId == siteId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(EngageChatSession session, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _sessions.InsertOneAsync(session, cancellationToken: cancellationToken);
    }

    public async Task TouchAsync(Guid sessionId, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var update = Builders<EngageChatSession>.Update.Set(item => item.UpdatedAtUtc, timestampUtc);
        await _sessions.UpdateOneAsync(item => item.Id == sessionId, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateStateAsync(EngageChatSession session, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var update = Builders<EngageChatSession>.Update
            .Set(item => item.ConversationState, session.ConversationState)
            .Set(item => item.PendingCaptureMode, session.PendingCaptureMode)
            .Set(item => item.IsConversationComplete, session.IsConversationComplete)
            .Set(item => item.LastCompletedAtUtc, session.LastCompletedAtUtc)
            .Set(item => item.LastAssistantAskType, session.LastAssistantAskType)
            .Set(item => item.CaptureGoal, session.CaptureGoal)
            .Set(item => item.CaptureContext, session.CaptureContext)
            .Set(item => item.CaptureType, session.CaptureType)
            .Set(item => item.CaptureLocation, session.CaptureLocation)
            .Set(item => item.CaptureConstraints, session.CaptureConstraints)
            .Set(item => item.CapturedName, session.CapturedName)
            .Set(item => item.CapturedEmail, session.CapturedEmail)
            .Set(item => item.CapturedPhone, session.CapturedPhone)
            .Set(item => item.CapturedPreferredContactMethod, session.CapturedPreferredContactMethod)
            .Set(item => item.OpportunityLabel, session.OpportunityLabel)
            .Set(item => item.IntentScore, session.IntentScore)
            .Set(item => item.ConversationSummary, session.ConversationSummary)
            .Set(item => item.SuggestedFollowUp, session.SuggestedFollowUp)
            .Set(item => item.UpdatedAtUtc, session.UpdatedAtUtc);
        await _sessions.UpdateOneAsync(item => item.Id == session.Id, update, cancellationToken: cancellationToken);
    }

    public async Task SetCollectorSessionIdIfEmptyAsync(Guid sessionId, string collectorSessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var normalized = collectorSessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var filter = Builders<EngageChatSession>.Filter.Eq(item => item.Id, sessionId)
            & (Builders<EngageChatSession>.Filter.Eq(item => item.CollectorSessionId, null)
               | Builders<EngageChatSession>.Filter.Eq(item => item.CollectorSessionId, string.Empty));

        var update = Builders<EngageChatSession>.Update.Set(item => item.CollectorSessionId, normalized);
        await _sessions.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<EngageChatSession>> ListBySiteAsync(Guid tenantId, Guid siteId, string? collectorSessionId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var filter = Builders<EngageChatSession>.Filter.Eq(item => item.TenantId, tenantId)
            & Builders<EngageChatSession>.Filter.Eq(item => item.SiteId, siteId);

        if (!string.IsNullOrWhiteSpace(collectorSessionId))
        {
            filter &= Builders<EngageChatSession>.Filter.Eq(item => item.CollectorSessionId, collectorSessionId.Trim());
        }

        var results = await _sessions.Find(filter)
            .SortByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        return results;
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<EngageChatSession>(
                Builders<EngageChatSession>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId)),
            new CreateIndexModel<EngageChatSession>(Builders<EngageChatSession>.IndexKeys.Descending(item => item.UpdatedAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_sessions, indexes);
    }
}
