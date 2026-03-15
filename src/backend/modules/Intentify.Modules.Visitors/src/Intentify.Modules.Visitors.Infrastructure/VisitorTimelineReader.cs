using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Data.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Intentify.Modules.Visitors.Infrastructure;

public sealed class VisitorTimelineReader : IVisitorTimelineReader
{
    private readonly IMongoCollection<CollectorEvent> _events;
    private readonly Task _ensureIndexes;

    public VisitorTimelineReader(IMongoDatabase database)
    {
        _events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<IReadOnlyCollection<VisitorTimelineItem>> GetTimelineAsync(VisitorTimelineQuery query, IReadOnlyCollection<string> sessionIds, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0)
        {
            return Array.Empty<VisitorTimelineItem>();
        }

        await _ensureIndexes;

        var filter = Builders<CollectorEvent>.Filter.Eq(item => item.TenantId, query.TenantId)
            & Builders<CollectorEvent>.Filter.Eq(item => item.SiteId, query.SiteId)
            & Builders<CollectorEvent>.Filter.In(item => item.SessionId, sessionIds);

        if (retentionFloorUtc is { } floor)
        {
            filter &= Builders<CollectorEvent>.Filter.Gte(item => item.OccurredAtUtc, floor);
        }

        var events = await _events.Find(filter)
            .SortByDescending(item => item.OccurredAtUtc)
            .Limit(query.Limit)
            .ToListAsync(cancellationToken);

        return events.Select(item => new VisitorTimelineItem(
            item.OccurredAtUtc,
            item.Type,
            item.SessionId,
            item.Url,
            item.Referrer,
            ToSummary(item.Data))).ToArray();
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<CollectorEvent>(
                Builders<CollectorEvent>.IndexKeys
                    .Ascending(item => item.SiteId)
                    .Ascending(item => item.SessionId)
                    .Descending(item => item.OccurredAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_events, indexes);
    }

    private static IReadOnlyDictionary<string, string>? ToSummary(BsonDocument? data)
    {
        if (data is null || data.ElementCount == 0)
        {
            return null;
        }

        var summary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in data.Elements)
        {
            if (summary.Count == 8)
            {
                break;
            }

            if (element.Value.IsString)
            {
                summary[element.Name] = element.Value.AsString;
            }
            else if (element.Value.IsNumeric)
            {
                summary[element.Name] = element.Value.ToString();
            }
            else if (element.Value.IsBoolean)
            {
                summary[element.Name] = element.Value.AsBoolean ? "true" : "false";
            }
        }

        return summary.Count == 0 ? null : summary;
    }
}
