using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Intentify.Modules.Visitors.Infrastructure;

public sealed class VisitorAnalyticsReader : IVisitorAnalyticsReader
{
    private readonly IMongoCollection<Visitor> _visitors;
    private readonly IMongoCollection<CollectorEvent> _events;
    private readonly Task _ensureVisitorIndexes;
    private readonly Task _ensureEventIndexes;

    public VisitorAnalyticsReader(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        _events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        _ensureVisitorIndexes = EnsureVisitorIndexesAsync();
        _ensureEventIndexes = EnsureEventIndexesAsync();
    }

    public async Task<IReadOnlyCollection<OnlineVisitorItem>> GetOnlineNowAsync(Guid tenantId, Guid siteId, DateTime cutoffUtc, int limit, CancellationToken cancellationToken = default)
    {
        await _ensureVisitorIndexes;

        var visitors = await _visitors.Find(visitor => visitor.TenantId == tenantId && visitor.SiteId == siteId)
            .ToListAsync(cancellationToken);

        var items = visitors
            .Select(visitor =>
            {
                var activeSessions = visitor.Sessions
                    .Where(session => session.LastSeenAtUtc >= cutoffUtc)
                    .OrderByDescending(session => session.LastSeenAtUtc)
                    .ToArray();

                if (activeSessions.Length == 0)
                {
                    return null;
                }

                var latest = activeSessions[0];
                return new OnlineVisitorItem(
                    visitor.Id,
                    latest.LastSeenAtUtc,
                    activeSessions.Length,
                    latest.LastPath,
                    latest.LastReferrer);
            })
            .Where(item => item is not null)
            .Cast<OnlineVisitorItem>()
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Take(limit)
            .ToArray();

        return items;
    }

    public async Task<IReadOnlyCollection<PageAnalyticsItem>> GetTopPagesAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, int limit, CancellationToken cancellationToken = default)
    {
        await _ensureEventIndexes;

        var filter = Builders<CollectorEvent>.Filter.Eq(item => item.TenantId, tenantId)
            & Builders<CollectorEvent>.Filter.Eq(item => item.SiteId, siteId)
            & Builders<CollectorEvent>.Filter.Gte(item => item.OccurredAtUtc, sinceUtc)
            & Builders<CollectorEvent>.Filter.In(item => item.Type, new[] { "pageview", "time_on_page" });

        var events = await _events.Find(filter)
            .Project(item => new CollectorEventProjection(item.Type, item.Url, item.SessionId, item.Data))
            .ToListAsync(cancellationToken);

        var byPage = new Dictionary<string, PageAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in events)
        {
            var page = NormalizePage(item.Url);
            if (string.IsNullOrWhiteSpace(page))
            {
                continue;
            }

            if (!byPage.TryGetValue(page, out var aggregate))
            {
                aggregate = new PageAggregate();
                byPage[page] = aggregate;
            }

            if (string.Equals(item.Type, "pageview", StringComparison.OrdinalIgnoreCase))
            {
                aggregate.PageViews += 1;
                if (!string.IsNullOrWhiteSpace(item.SessionId))
                {
                    aggregate.UniqueSessions.Add(item.SessionId);
                }
            }
            else if (string.Equals(item.Type, "time_on_page", StringComparison.OrdinalIgnoreCase))
            {
                var seconds = TryResolveTimeOnPageSeconds(item.Data);
                if (seconds is > 0)
                {
                    aggregate.TimeOnPageSecondsTotal += seconds.Value;
                    aggregate.TimeOnPageSamples += 1;
                }
            }
        }

        return byPage
            .Select(pair => new PageAnalyticsItem(
                pair.Key,
                pair.Value.PageViews,
                pair.Value.UniqueSessions.Count,
                pair.Value.TimeOnPageSamples > 0
                    ? Math.Round((decimal)pair.Value.TimeOnPageSecondsTotal / pair.Value.TimeOnPageSamples, 2)
                    : 0))
            .OrderByDescending(item => item.PageViews)
            .ThenBy(item => item.PageUrl, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static string NormalizePage(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return rawUrl.Trim();
        }

        var path = string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        return path;
    }

    private static decimal? TryResolveTimeOnPageSeconds(BsonDocument? data)
    {
        if (data is null)
        {
            return null;
        }

        if (!data.TryGetValue("seconds", out var value))
        {
            return null;
        }

        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return (decimal)value.AsDouble;
        if (value.IsDecimal128) return (decimal)value.AsDecimal128;
        if (value.IsString && decimal.TryParse(value.AsString, out var parsed)) return parsed;

        return null;
    }

    private Task EnsureVisitorIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Visitor>(
                Builders<Visitor>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId)
                    .Descending(item => item.LastSeenAtUtc)),
            new CreateIndexModel<Visitor>(
                Builders<Visitor>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId)
                    .Descending("Sessions.LastSeenAtUtc"))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_visitors, indexes);
    }

    private Task EnsureEventIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<CollectorEvent>(
                Builders<CollectorEvent>.IndexKeys
                    .Ascending(item => item.TenantId)
                    .Ascending(item => item.SiteId)
                    .Ascending(item => item.Type)
                    .Descending(item => item.OccurredAtUtc))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_events, indexes);
    }

    private sealed record CollectorEventProjection(string Type, string? Url, string? SessionId, BsonDocument? Data);

    private sealed class PageAggregate
    {
        public int PageViews { get; set; }
        public HashSet<string> UniqueSessions { get; } = new(StringComparer.Ordinal);
        public decimal TimeOnPageSecondsTotal { get; set; }
        public int TimeOnPageSamples { get; set; }
    }
}
