using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Intelligence.Application;
using MongoDB.Driver;

namespace Intentify.Modules.Intelligence.Infrastructure;

/// <summary>
/// Aggregates anonymous intent signals from CollectorEvents across ALL tenants
/// to produce network-level trends. Never returns individual visitor data —
/// only aggregated counts. Applies a minimum k-anonymity threshold (3 sites)
/// to prevent reverse-engineering individual tenants.
/// </summary>
public sealed class NetworkSignalsService : INetworkSignalsService
{
    private const int MinSitesThreshold = 3;
    private const int MaxDocuments = 1000;

    private readonly IMongoCollection<CollectorEvent> _events;

    public NetworkSignalsService(IMongoDatabase database)
    {
        _events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
    }

    public async Task<NetworkSignalsResult> GetNetworkSignalsAsync(
        NetworkSignalsQuery query,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(query.DaysBack, 1, 90));

        var filterBuilder = Builders<CollectorEvent>.Filter;
        var filter = filterBuilder.Gte(e => e.ReceivedAtUtc, since);

        if (!string.IsNullOrWhiteSpace(query.Country))
            filter &= filterBuilder.Eq(e => e.Country, query.Country);

        if (!string.IsNullOrWhiteSpace(query.ProductCategory))
            filter &= filterBuilder.Eq(e => e.ProductCategory, query.ProductCategory);

        // Project only anonymised fields — no VisitorId, TenantId, IP, personal data
        var projection = Builders<CollectorEvent>.Projection
            .Include(e => e.SiteId)
            .Include(e => e.Country)
            .Include(e => e.ProductName)
            .Include(e => e.ProductCategory)
            .Include(e => e.ProductPrice)
            .Include(e => e.PageType)
            .Exclude(e => e.Id)
            .Exclude("TenantId")
            .Exclude("VisitorId")
            .Exclude("IpAddress")
            .Exclude("Data");

        var events = await _events
            .Find(filter)
            .Project<EventSignalProjection>(projection)
            .Limit(MaxDocuments)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return new NetworkSignalsResult(
                [], [], [], [],
                0, 0,
                DateTime.UtcNow);
        }

        var allSiteIds = events.Select(e => e.SiteId).Distinct().ToHashSet();
        var totalSites = allSiteIds.Count;
        var totalEvents = events.Count;

        // ── Trending Topics (by ProductName) ────────────────────────────────
        var topicGroups = events
            .Where(e => !string.IsNullOrWhiteSpace(e.ProductName))
            .GroupBy(e => e.ProductName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Topic = g.Key,
                Count = g.Count(),
                Sites = g.Select(e => e.SiteId).Distinct().Count(),
                Category = g.Select(e => e.ProductCategory).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
            })
            .Where(g => g.Sites >= MinSitesThreshold)
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToList();

        var maxTopicCount = topicGroups.Count > 0 ? topicGroups.Max(t => t.Count) : 1;
        var trendingTopics = topicGroups.Select(t => new TrendingTopicSignal(
            t.Topic,
            t.Count,
            Math.Round((double)t.Count / maxTopicCount, 3),
            t.Category)).ToList();

        // ── Category Intents ─────────────────────────────────────────────────
        var categoryGroups = events
            .Where(e => !string.IsNullOrWhiteSpace(e.ProductCategory))
            .GroupBy(e => e.ProductCategory!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Category = g.Key,
                VisitorCount = g.Count(),
                SiteCount = g.Select(e => e.SiteId).Distinct().Count(),
            })
            .Where(g => g.SiteCount >= MinSitesThreshold)
            .OrderByDescending(g => g.VisitorCount)
            .Take(10)
            .ToList();

        var categoryIntents = categoryGroups.Select(c => new CategoryIntentSignal(
            c.Category,
            c.VisitorCount,
            c.SiteCount,
            totalSites > 0 ? Math.Round((double)c.SiteCount / totalSites, 3) : 0)).ToList();

        // ── Country Intents ──────────────────────────────────────────────────
        var countryGroups = events
            .Where(e => !string.IsNullOrWhiteSpace(e.Country))
            .GroupBy(e => e.Country!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Country = g.Key,
                VisitorCount = g.Count(),
                Sites = g.Select(e => e.SiteId).Distinct().Count(),
            })
            .Where(g => g.Sites >= MinSitesThreshold)
            .OrderByDescending(g => g.VisitorCount)
            .Take(10)
            .ToList();

        var maxCountryCount = countryGroups.Count > 0 ? countryGroups.Max(c => c.VisitorCount) : 1;
        var countryIntents = countryGroups.Select(c => new CountryIntentSignal(
            c.Country,
            c.VisitorCount,
            totalEvents > 0 ? Math.Round((double)c.VisitorCount / totalEvents, 3) : 0)).ToList();

        // ── Product Trends ───────────────────────────────────────────────────
        var productGroups = events
            .Where(e => !string.IsNullOrWhiteSpace(e.ProductName))
            .GroupBy(e => e.ProductName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                ProductName = g.Key,
                ViewCount = g.Count(),
                Sites = g.Select(e => e.SiteId).Distinct().Count(),
                Category = g.Select(e => e.ProductCategory).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                Prices = g.Select(e => e.ProductPrice)
                          .Where(p => !string.IsNullOrWhiteSpace(p))
                          .ToList(),
            })
            .Where(g => g.Sites >= MinSitesThreshold)
            .OrderByDescending(g => g.ViewCount)
            .Take(10)
            .ToList();

        var productTrends = productGroups.Select(p => new ProductTrendSignal(
            p.ProductName,
            p.ViewCount,
            p.Category,
            InferPriceRange(p.Prices))).ToList();

        return new NetworkSignalsResult(
            trendingTopics,
            categoryIntents,
            countryIntents,
            productTrends,
            totalSites,
            totalEvents,
            DateTime.UtcNow);
    }

    private static string? InferPriceRange(List<string?> prices)
    {
        if (prices.Count == 0) return null;
        var numeric = prices
            .Select(p => decimal.TryParse(p, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? (decimal?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (numeric.Count == 0) return null;
        var avg = (double)numeric.Average();
        return avg switch
        {
            < 50    => "Under £50",
            < 200   => "£50–£200",
            < 1000  => "£200–£1000",
            _       => "£1000+",
        };
    }

    // Minimal projection type — only the fields we need
    private sealed class EventSignalProjection
    {
        public Guid SiteId { get; init; }
        public string? Country { get; init; }
        public string? ProductName { get; init; }
        public string? ProductCategory { get; init; }
        public string? ProductPrice { get; init; }
        public string? PageType { get; init; }
    }
}
