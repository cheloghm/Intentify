using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Intentify.Modules.Visitors.Infrastructure;

// Implements the interface declared in Application — no circular reference.
public sealed class DashboardAnalyticsReader : IDashboardAnalyticsReader
{
    private readonly IMongoCollection<Visitor> _visitors;
    private readonly IMongoCollection<CollectorEvent> _events;
    private readonly Task _ensureIndexes;

    public DashboardAnalyticsReader(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        _events   = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<DashboardAnalyticsResult> GetDashboardAsync(DashboardAnalyticsQuery query, CancellationToken ct = default)
    {
        await _ensureIndexes;

        var now      = DateTime.UtcNow;
        var todayUtc = now.Date;
        var cutoff5m = now.AddMinutes(-5);
        var week0    = todayUtc.AddDays(-7);
        var week1    = todayUtc.AddDays(-14);
        var days14   = todayUtc.AddDays(-14);

        // ── All visitors active in last 14 days ───────────────────────────────
        var allVisitors14 = await _visitors
            .Find(v => v.TenantId == query.TenantId && v.SiteId == query.SiteId
                    && v.LastSeenAtUtc >= days14)
            .ToListAsync(ct);

        // Today
        var todayVisitors = allVisitors14.Count(v => v.LastSeenAtUtc >= todayUtc);
        var todaySessions = allVisitors14.Sum(v => v.Sessions.Count(s => s.LastSeenAtUtc >= todayUtc));

        // Online now (active in last 5 mins)
        var onlineNow = allVisitors14.Count(v => v.Sessions.Any(s => s.LastSeenAtUtc >= cutoff5m));

        // Week comparison
        var weekVisitors     = allVisitors14.Count(v => v.LastSeenAtUtc >= week0);
        var lastWeekVisitors = allVisitors14.Count(v => v.LastSeenAtUtc >= week1 && v.LastSeenAtUtc < week0);
        var weekChange       = lastWeekVisitors == 0 ? 0d
            : Math.Round((weekVisitors - lastWeekVisitors) / (double)lastWeekVisitors * 100, 1);

        // Avg session time + bounce rate (sessions in last 7 days)
        var recentSessions = allVisitors14
            .SelectMany(v => v.Sessions.Where(s => s.FirstSeenAtUtc >= week0))
            .ToArray();

        var avgSessionSeconds = recentSessions.Length == 0 ? 0d : recentSessions.Average(s => s.TimeOnSiteSeconds);
        var bounceRate = recentSessions.Length == 0 ? 0d
            : Math.Round((double)recentSessions.Count(s => s.PagesVisited <= 1) / recentSessions.Length * 100, 1);

        // ── Daily trend ───────────────────────────────────────────────────────
        var last14 = Enumerable.Range(0, 14).Select(i =>
        {
            var day    = todayUtc.AddDays(-13 + i);
            var dayEnd = day.AddDays(1);
            var vis    = allVisitors14.Count(v => v.LastSeenAtUtc >= day && v.LastSeenAtUtc < dayEnd);
            var sess   = allVisitors14.Sum(v => v.Sessions.Count(s => s.FirstSeenAtUtc >= day && s.FirstSeenAtUtc < dayEnd));
            return new DailyVisitorPoint(day.ToString("MMM d"), vis, sess);
        }).ToArray();

        // ── Top pages from collector events (last 7 days) ─────────────────────
        var evFilter = Builders<CollectorEvent>.Filter.Eq(e => e.TenantId, query.TenantId)
            & Builders<CollectorEvent>.Filter.Eq(e => e.SiteId, query.SiteId)
            & Builders<CollectorEvent>.Filter.Gte(e => e.OccurredAtUtc, week0)
            & Builders<CollectorEvent>.Filter.In(e => e.Type, new[] { "pageview", "time_on_page" });

        var events = await _events.Find(evFilter)
            .Project(e => new { e.Type, e.Url, e.SessionId, e.Data })
            .ToListAsync(ct);

        var byPage = new Dictionary<string, PageAgg>(StringComparer.OrdinalIgnoreCase);
        foreach (var ev in events)
        {
            var page = NormUrl(ev.Url); if (string.IsNullOrWhiteSpace(page)) continue;
            if (!byPage.TryGetValue(page, out var agg)) { agg = new PageAgg(); byPage[page] = agg; }
            if (ev.Type == "pageview")
            {
                agg.Views++;
                if (!string.IsNullOrWhiteSpace(ev.SessionId)) agg.Sessions.Add(ev.SessionId);
            }
            else if (ev.Type == "time_on_page")
            {
                var secs = ResolveSeconds(ev.Data);
                if (secs > 0) { agg.TotalSecs += secs; agg.SecsSamples++; }
            }
        }

        // Per-page bounce rate
        var sessionPageCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ev in events.Where(e => e.Type == "pageview" && !string.IsNullOrWhiteSpace(e.SessionId)))
        {
            var page = NormUrl(ev.Url); if (string.IsNullOrWhiteSpace(page)) continue;
            if (!sessionPageCounts.TryGetValue(ev.SessionId!, out var pp)) { pp = new(); sessionPageCounts[ev.SessionId!] = pp; }
            pp[page] = pp.TryGetValue(page, out var c) ? c + 1 : 1;
        }

        var topPages = byPage.Select(kv =>
        {
            var bounced = kv.Value.Sessions.Count(sid =>
                sessionPageCounts.TryGetValue(sid, out var pp) && pp.Count <= 1);
            var pageBounce = kv.Value.Sessions.Count == 0 ? 0d
                : Math.Round((double)bounced / kv.Value.Sessions.Count * 100, 1);
            return new TopPageItem(
                kv.Key, kv.Value.Views, kv.Value.Sessions.Count,
                kv.Value.SecsSamples > 0 ? Math.Round(kv.Value.TotalSecs / kv.Value.SecsSamples, 1) : 0d,
                pageBounce);
        }).OrderByDescending(p => p.PageViews).Take(10).ToArray();

        // ── Country breakdown ──────────────────────────────────────────────────
        var total = allVisitors14.Count;
        var topCountries = allVisitors14
            .GroupBy(v => string.IsNullOrWhiteSpace(v.Country) ? "Unknown" : v.Country, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CountryBreakdownItem(g.Key, g.Count(),
                total == 0 ? 0d : Math.Round((double)g.Count() / total * 100, 1)))
            .OrderByDescending(c => c.VisitorCount).Take(8).ToArray();

        return new DashboardAnalyticsResult(
            todayVisitors, todaySessions, onlineNow,
            weekVisitors, lastWeekVisitors, weekChange,
            Math.Round(avgSessionSeconds, 1), bounceRate,
            last14, topPages, topCountries);
    }

    private static string NormUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var u)) return raw.Trim();
        return string.IsNullOrWhiteSpace(u.PathAndQuery) ? "/" : u.PathAndQuery;
    }

    private static double ResolveSeconds(BsonDocument? data)
    {
        if (data is null || !data.TryGetValue("seconds", out var v)) return 0;
        if (v.IsInt32) return v.AsInt32;
        if (v.IsInt64) return v.AsInt64;
        if (v.IsDouble) return v.AsDouble;
        if (v.IsString && double.TryParse(v.AsString, out var p)) return p;
        return 0;
    }

    private Task EnsureIndexesAsync() =>
        MongoIndexHelper.EnsureIndexesAsync(_events, new[]
        {
            new CreateIndexModel<CollectorEvent>(Builders<CollectorEvent>.IndexKeys
                .Ascending(e => e.TenantId).Ascending(e => e.SiteId)
                .Ascending(e => e.Type).Descending(e => e.OccurredAtUtc))
        });

    private sealed class PageAgg
    {
        public int Views { get; set; }
        public HashSet<string> Sessions { get; } = new(StringComparer.Ordinal);
        public double TotalSecs { get; set; }
        public int SecsSamples { get; set; }
    }
}
