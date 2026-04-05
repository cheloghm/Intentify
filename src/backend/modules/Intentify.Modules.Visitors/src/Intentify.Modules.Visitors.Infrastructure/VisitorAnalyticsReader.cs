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
        _events   = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        _ensureVisitorIndexes = EnsureVisitorIndexesAsync();
        _ensureEventIndexes   = EnsureEventIndexesAsync();
    }

    // ── Online Now ────────────────────────────────────────────────────────────
    // Returns visitors who have been seen within the cutoff window.
    // Country and Platform come from the Visitor document (enriched on ingest).

    public async Task<IReadOnlyCollection<OnlineVisitorItem>> GetOnlineNowAsync(
        Guid tenantId, Guid siteId, DateTime cutoffUtc, int limit,
        CancellationToken cancellationToken = default)
    {
        await _ensureVisitorIndexes;

        var visitors = await _visitors
            .Find(v => v.TenantId == tenantId && v.SiteId == siteId)
            .ToListAsync(cancellationToken);

        return visitors
            .Select(v =>
            {
                var active = v.Sessions
                    .Where(s => s.LastSeenAtUtc >= cutoffUtc)
                    .OrderByDescending(s => s.LastSeenAtUtc)
                    .ToArray();

                if (active.Length == 0) return null;

                var latest = active[0];
                return new OnlineVisitorItem(
                    v.Id,
                    latest.LastSeenAtUtc,
                    active.Length,
                    latest.LastPath,
                    latest.LastReferrer,
                    Country:      v.Country,
                    Platform:     v.Platform,
                    PrimaryEmail: v.PrimaryEmail);
            })
            .Where(item => item is not null)
            .Cast<OnlineVisitorItem>()
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Take(limit)
            .ToArray();
    }

    // ── Top Pages ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<PageAnalyticsItem>> GetTopPagesAsync(
        Guid tenantId, Guid siteId, DateTime sinceUtc, int limit,
        CancellationToken cancellationToken = default)
    {
        await _ensureEventIndexes;

        var filter = Builders<CollectorEvent>.Filter.Eq(e => e.TenantId, tenantId)
            & Builders<CollectorEvent>.Filter.Eq(e => e.SiteId, siteId)
            & Builders<CollectorEvent>.Filter.Gte(e => e.OccurredAtUtc, sinceUtc)
            & Builders<CollectorEvent>.Filter.In(e => e.Type, new[] { "pageview", "time_on_page" });

        var events = await _events.Find(filter)
            .Project(e => new CollectorEventProjection(e.Type, e.Url, e.SessionId, e.Data))
            .ToListAsync(cancellationToken);

        var byPage = new Dictionary<string, PageAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in events)
        {
            var page = NormalizePage(item.Url);
            if (string.IsNullOrWhiteSpace(page)) continue;

            if (!byPage.TryGetValue(page, out var agg)) { agg = new PageAggregate(); byPage[page] = agg; }

            if (string.Equals(item.Type, "pageview", StringComparison.OrdinalIgnoreCase))
            {
                agg.PageViews++;
                if (!string.IsNullOrWhiteSpace(item.SessionId)) agg.UniqueSessions.Add(item.SessionId);
            }
            else if (string.Equals(item.Type, "time_on_page", StringComparison.OrdinalIgnoreCase))
            {
                var secs = TryResolveSeconds(item.Data);
                if (secs is > 0) { agg.TimeOnPageSecondsTotal += secs.Value; agg.TimeOnPageSamples++; }
            }
        }

        return byPage
            .Select(kv => new PageAnalyticsItem(
                kv.Key, kv.Value.PageViews, kv.Value.UniqueSessions.Count,
                kv.Value.TimeOnPageSamples > 0
                    ? Math.Round((decimal)kv.Value.TimeOnPageSecondsTotal / kv.Value.TimeOnPageSamples, 2)
                    : 0))
            .OrderByDescending(p => p.PageViews)
            .ThenBy(p => p.PageUrl, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    // ── Country Breakdown (Phase 2) ───────────────────────────────────────────
    // Aggregates visitor counts by country from the visitors collection.

    public async Task<IReadOnlyCollection<CountryBreakdownItem>> GetCountryBreakdownAsync(
        Guid tenantId, Guid siteId, DateTime sinceUtc, int limit,
        CancellationToken cancellationToken = default)
    {
        await _ensureVisitorIndexes;

        var visitors = await _visitors
            .Find(v => v.TenantId == tenantId && v.SiteId == siteId
                    && v.LastSeenAtUtc >= sinceUtc)
            .ToListAsync(cancellationToken);

        var total = visitors.Count;
        if (total == 0) return [];

        return visitors
            .GroupBy(v => string.IsNullOrWhiteSpace(v.Country) ? "Unknown" : v.Country,
                     StringComparer.OrdinalIgnoreCase)
            .Select(g => new CountryBreakdownItem(
                g.Key,
                g.Count(),
                Math.Round((double)g.Count() / total * 100, 1)))
            .OrderByDescending(c => c.VisitorCount)
            .Take(limit)
            .ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizePage(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;
        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)) return rawUrl.Trim();
        return string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
    }

    private static decimal? TryResolveSeconds(BsonDocument? data)
    {
        if (data is null || !data.TryGetValue("seconds", out var v)) return null;
        if (v.IsInt32)  return v.AsInt32;
        if (v.IsInt64)  return v.AsInt64;
        if (v.IsDouble) return (decimal)v.AsDouble;
        if (v.IsDecimal128) return (decimal)v.AsDecimal128;
        if (v.IsString && decimal.TryParse(v.AsString, out var p)) return p;
        return null;
    }

    private Task EnsureVisitorIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Visitor>(Builders<Visitor>.IndexKeys
                .Ascending(v => v.TenantId).Ascending(v => v.SiteId)
                .Descending(v => v.LastSeenAtUtc)),
            new CreateIndexModel<Visitor>(Builders<Visitor>.IndexKeys
                .Ascending(v => v.TenantId).Ascending(v => v.SiteId)
                .Descending("Sessions.LastSeenAtUtc")),
            // Phase 2: index for country breakdown queries
            new CreateIndexModel<Visitor>(Builders<Visitor>.IndexKeys
                .Ascending(v => v.TenantId).Ascending(v => v.SiteId)
                .Ascending(v => v.Country))
        };
        return MongoIndexHelper.EnsureIndexesAsync(_visitors, indexes);
    }

    private Task EnsureEventIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<CollectorEvent>(Builders<CollectorEvent>.IndexKeys
                .Ascending(e => e.TenantId).Ascending(e => e.SiteId)
                .Ascending(e => e.Type).Descending(e => e.OccurredAtUtc))
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
