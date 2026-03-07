using Intentify.Modules.Ads.Domain;
using Intentify.Modules.Auth.Domain;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Knowledge.Infrastructure;
using Intentify.Modules.PlatformAdmin.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Modules.Sites.Domain;
using Intentify.Modules.Tickets.Domain;
using Intentify.Modules.Visitors.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Intentify.Modules.PlatformAdmin.Infrastructure;

public sealed class PlatformAdminReadRepository : IPlatformAdminReadRepository
{
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly IMongoCollection<Site> _sites;
    private readonly IMongoCollection<Visitor> _visitors;
    private readonly IMongoCollection<EngageChatSession> _engageSessions;
    private readonly IMongoCollection<EngageChatMessage> _engageMessages;
    private readonly IMongoCollection<Ticket> _tickets;
    private readonly IMongoCollection<Promo> _promos;
    private readonly IMongoCollection<PromoEntry> _promoEntries;
    private readonly IMongoCollection<IntelligenceTrendRecord> _intelligenceRecords;
    private readonly IMongoCollection<AdCampaign> _adsCampaigns;
    private readonly IMongoCollection<KnowledgeSource> _knowledgeSources;
    private readonly OpenSearchOptions _openSearchOptions;

    public PlatformAdminReadRepository(IMongoDatabase database, OpenSearchOptions openSearchOptions)
    {
        _tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
        _sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        _engageSessions = database.GetCollection<EngageChatSession>(EngageMongoCollections.ChatSessions);
        _engageMessages = database.GetCollection<EngageChatMessage>(EngageMongoCollections.ChatMessages);
        _tickets = database.GetCollection<Ticket>(TicketsMongoCollections.Tickets);
        _promos = database.GetCollection<Promo>(PromosMongoCollections.Promos);
        _promoEntries = database.GetCollection<PromoEntry>(PromosMongoCollections.PromoEntries);
        _intelligenceRecords = database.GetCollection<IntelligenceTrendRecord>(IntelligenceMongoCollections.Trends);
        _adsCampaigns = database.GetCollection<AdCampaign>(AdsMongoCollections.Campaigns);
        _knowledgeSources = database.GetCollection<KnowledgeSource>(KnowledgeMongoCollections.Sources);
        _openSearchOptions = openSearchOptions;
    }

    public async Task<PlatformSummaryResult> GetPlatformSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalTenants = (int)await _tenants.CountDocumentsAsync(FilterDefinition<Tenant>.Empty, cancellationToken: cancellationToken);
        var totalSites = (int)await _sites.CountDocumentsAsync(FilterDefinition<Site>.Empty, cancellationToken: cancellationToken);
        var totalVisitors = (int)await _visitors.CountDocumentsAsync(FilterDefinition<Visitor>.Empty, cancellationToken: cancellationToken);
        var totalEngageSessions = (int)await _engageSessions.CountDocumentsAsync(FilterDefinition<EngageChatSession>.Empty, cancellationToken: cancellationToken);
        var totalEngageMessages = (int)await _engageMessages.CountDocumentsAsync(FilterDefinition<EngageChatMessage>.Empty, cancellationToken: cancellationToken);
        var totalTickets = (int)await _tickets.CountDocumentsAsync(FilterDefinition<Ticket>.Empty, cancellationToken: cancellationToken);
        var totalPromos = (int)await _promos.CountDocumentsAsync(FilterDefinition<Promo>.Empty, cancellationToken: cancellationToken);
        var totalPromoEntries = (int)await _promoEntries.CountDocumentsAsync(FilterDefinition<PromoEntry>.Empty, cancellationToken: cancellationToken);
        var totalIntelligenceRecords = (int)await _intelligenceRecords.CountDocumentsAsync(FilterDefinition<IntelligenceTrendRecord>.Empty, cancellationToken: cancellationToken);
        var totalKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(FilterDefinition<KnowledgeSource>.Empty, cancellationToken: cancellationToken);

        var indexedKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(
            source => source.Status == IndexStatus.Indexed,
            cancellationToken: cancellationToken);

        var failedKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(
            source => source.Status == IndexStatus.Failed,
            cancellationToken: cancellationToken);

        return new PlatformSummaryResult(
            totalTenants,
            totalSites,
            totalVisitors,
            totalEngageSessions,
            totalEngageMessages,
            totalTickets,
            totalPromos,
            totalPromoEntries,
            totalIntelligenceRecords,
            totalKnowledgeSources,
            indexedKnowledgeSources,
            failedKnowledgeSources,
            "ok",
            DateTime.UtcNow);
    }

    public async Task<PlatformTenantListResult> ListTenantsAsync(ListPlatformTenantsQuery query, CancellationToken cancellationToken = default)
    {
        var tenants = await _tenants.Find(FilterDefinition<Tenant>.Empty)
            .SortBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var filtered = string.IsNullOrWhiteSpace(query.Search)
            ? tenants
            : tenants.Where(item =>
                    item.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                    || item.Domain.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var totalCount = filtered.Count;
        var paged = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var tenantIds = paged.Select(item => item.Id).ToArray();
        var usage = await BuildUsageByTenantAsync(tenantIds, cancellationToken);

        var rows = paged.Select(item =>
        {
            usage.TryGetValue(item.Id, out var usageItem);
            usageItem ??= EmptyUsage();

            return new PlatformTenantListRowResult(
                item.Id,
                item.Name,
                item.Domain,
                item.Plan,
                item.Industry,
                item.Category,
                item.CreatedAt,
                item.UpdatedAt,
                usageItem);
        }).ToArray();

        return new PlatformTenantListResult(query.Page, query.PageSize, totalCount, rows);
    }

    public async Task<PlatformTenantDetailResult?> GetTenantDetailAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.Find(item => item.Id == tenantId).FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var usageByTenant = await BuildUsageByTenantAsync([tenantId], cancellationToken);
        var usage = usageByTenant.TryGetValue(tenantId, out var value) ? value : EmptyUsage();

        var sites = await _sites.Find(item => item.TenantId == tenantId)
            .SortBy(item => item.Domain)
            .Project(item => new PlatformTenantSiteResult(
                item.Id,
                item.Domain,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.FirstEventReceivedAtUtc))
            .ToListAsync(cancellationToken);

        var recentActivity = await BuildRecentActivityAsync(tenantId, cancellationToken);

        return new PlatformTenantDetailResult(
            tenant.Id,
            tenant.Name,
            tenant.Domain,
            tenant.Plan,
            tenant.Industry,
            tenant.Category,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            usage,
            recentActivity,
            sites);
    }

    public async Task<PlatformOperationalSummaryResult> GetOperationalSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(FilterDefinition<KnowledgeSource>.Empty, cancellationToken: cancellationToken);
        var indexedKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(source => source.Status == IndexStatus.Indexed, cancellationToken: cancellationToken);
        var failedKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(source => source.Status == IndexStatus.Failed, cancellationToken: cancellationToken);
        var queuedKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(source => source.Status == IndexStatus.Queued, cancellationToken: cancellationToken);
        var processingKnowledgeSources = (int)await _knowledgeSources.CountDocumentsAsync(source => source.Status == IndexStatus.Processing, cancellationToken: cancellationToken);

        var openSearchConfigured = !string.IsNullOrWhiteSpace(_openSearchOptions.Url)
            && !string.IsNullOrWhiteSpace(_openSearchOptions.IndexName);

        return new PlatformOperationalSummaryResult(
            "ok",
            totalKnowledgeSources,
            indexedKnowledgeSources,
            failedKnowledgeSources,
            queuedKnowledgeSources,
            processingKnowledgeSources,
            _openSearchOptions.Enabled,
            openSearchConfigured,
            DateTime.UtcNow);
    }

    private async Task<Dictionary<Guid, PlatformTenantUsageResult>> BuildUsageByTenantAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, PlatformTenantUsageResult>();
        if (tenantIds.Count == 0)
        {
            return result;
        }

        var siteCounts = await CountByTenantAsync(_sites, tenantIds, nameof(Site.TenantId), cancellationToken);
        var visitorCounts = await CountByTenantAsync(_visitors, tenantIds, nameof(Visitor.TenantId), cancellationToken);
        var engageSessionCounts = await CountByTenantAsync(_engageSessions, tenantIds, nameof(EngageChatSession.TenantId), cancellationToken);
        var engageMessageCounts = await CountEngageMessagesByTenantAsync(tenantIds, cancellationToken);
        var ticketCounts = await CountByTenantAsync(_tickets, tenantIds, nameof(Ticket.TenantId), cancellationToken);
        var promoCounts = await CountByTenantAsync(_promos, tenantIds, nameof(Promo.TenantId), cancellationToken);
        var promoEntryCounts = await CountByTenantAsync(_promoEntries, tenantIds, nameof(PromoEntry.TenantId), cancellationToken);
        var intelligenceCounts = await CountByTenantAsync(_intelligenceRecords, tenantIds, nameof(IntelligenceTrendRecord.TenantId), cancellationToken);
        var adsCounts = await CountByTenantAsync(_adsCampaigns, tenantIds, nameof(AdCampaign.TenantId), cancellationToken);
        var knowledgeCounts = await CountByTenantAsync(_knowledgeSources, tenantIds, nameof(KnowledgeSource.TenantId), cancellationToken);
        var indexedKnowledgeCounts = await CountKnowledgeByStatusAsync(tenantIds, IndexStatus.Indexed, cancellationToken);
        var failedKnowledgeCounts = await CountKnowledgeByStatusAsync(tenantIds, IndexStatus.Failed, cancellationToken);

        var lastSiteActivity = await MaxDateByTenantAsync(_sites, tenantIds, nameof(Site.TenantId), nameof(Site.UpdatedAtUtc), cancellationToken);
        var lastVisitorActivity = await MaxDateByTenantAsync(_visitors, tenantIds, nameof(Visitor.TenantId), nameof(Visitor.LastSeenAtUtc), cancellationToken);
        var lastEngageSessionActivity = await MaxDateByTenantAsync(_engageSessions, tenantIds, nameof(EngageChatSession.TenantId), nameof(EngageChatSession.UpdatedAtUtc), cancellationToken);
        var lastTicketActivity = await MaxDateByTenantAsync(_tickets, tenantIds, nameof(Ticket.TenantId), nameof(Ticket.UpdatedAtUtc), cancellationToken);
        var lastPromoActivity = await MaxDateByTenantAsync(_promos, tenantIds, nameof(Promo.TenantId), nameof(Promo.UpdatedAtUtc), cancellationToken);
        var lastPromoEntryActivity = await MaxDateByTenantAsync(_promoEntries, tenantIds, nameof(PromoEntry.TenantId), nameof(PromoEntry.CreatedAtUtc), cancellationToken);
        var lastIntelligenceActivity = await MaxDateByTenantAsync(_intelligenceRecords, tenantIds, nameof(IntelligenceTrendRecord.TenantId), nameof(IntelligenceTrendRecord.RefreshedAtUtc), cancellationToken);
        var lastAdsActivity = await MaxDateByTenantAsync(_adsCampaigns, tenantIds, nameof(AdCampaign.TenantId), nameof(AdCampaign.UpdatedAtUtc), cancellationToken);
        var lastKnowledgeActivity = await MaxDateByTenantAsync(_knowledgeSources, tenantIds, nameof(KnowledgeSource.TenantId), nameof(KnowledgeSource.UpdatedAtUtc), cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            result[tenantId] = new PlatformTenantUsageResult(
                GetCount(siteCounts, tenantId),
                GetCount(visitorCounts, tenantId),
                GetCount(engageSessionCounts, tenantId),
                GetCount(engageMessageCounts, tenantId),
                GetCount(ticketCounts, tenantId),
                GetCount(promoCounts, tenantId),
                GetCount(promoEntryCounts, tenantId),
                GetCount(intelligenceCounts, tenantId),
                GetCount(adsCounts, tenantId),
                GetCount(knowledgeCounts, tenantId),
                GetCount(indexedKnowledgeCounts, tenantId),
                GetCount(failedKnowledgeCounts, tenantId),
                Max(
                    GetDate(lastSiteActivity, tenantId),
                    GetDate(lastVisitorActivity, tenantId),
                    GetDate(lastEngageSessionActivity, tenantId),
                    GetDate(lastTicketActivity, tenantId),
                    GetDate(lastPromoActivity, tenantId),
                    GetDate(lastPromoEntryActivity, tenantId),
                    GetDate(lastIntelligenceActivity, tenantId),
                    GetDate(lastAdsActivity, tenantId),
                    GetDate(lastKnowledgeActivity, tenantId)));
        }

        return result;
    }

    private async Task<PlatformTenantRecentActivityResult> BuildRecentActivityAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var site = await _sites.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var visitor = await _visitors.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.LastSeenAtUtc)
            .Project(item => (DateTime?)item.LastSeenAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var engageSession = await _engageSessions.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var ticket = await _tickets.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var promo = await _promos.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var promoEntry = await _promoEntries.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.CreatedAtUtc)
            .Project(item => (DateTime?)item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var intelligence = await _intelligenceRecords.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.RefreshedAtUtc)
            .Project(item => (DateTime?)item.RefreshedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var ads = await _adsCampaigns.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var knowledge = await _knowledgeSources.Find(item => item.TenantId == tenantId)
            .SortByDescending(item => item.UpdatedAtUtc)
            .Project(item => (DateTime?)item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new PlatformTenantRecentActivityResult(site, visitor, engageSession, ticket, promo, promoEntry, intelligence, ads, knowledge);
    }

    private async Task<Dictionary<Guid, int>> CountKnowledgeByStatusAsync(IReadOnlyCollection<Guid> tenantIds, IndexStatus status, CancellationToken cancellationToken)
    {
        var filter = Builders<KnowledgeSource>.Filter.In(item => item.TenantId, tenantIds)
            & Builders<KnowledgeSource>.Filter.Eq(item => item.Status, status);

        var aggregates = await _knowledgeSources.Aggregate()
            .Match(filter)
            .Group(item => item.TenantId, group => new { TenantId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return aggregates.ToDictionary(item => item.TenantId, item => item.Count);
    }

    private static async Task<Dictionary<Guid, int>> CountByTenantAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        IReadOnlyCollection<Guid> tenantIds,
        string tenantField,
        CancellationToken cancellationToken)
    {
        var filter = Builders<TDocument>.Filter.In(tenantField, tenantIds);

        var grouped = await collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", "$" + tenantField },
                { "count", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var item in grouped)
        {
            var id = item["_id"].AsGuid;
            result[id] = item["count"].ToInt32();
        }

        return result;
    }

    private async Task<Dictionary<Guid, int>> CountEngageMessagesByTenantAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", EngageMongoCollections.ChatSessions },
                { "localField", nameof(EngageChatMessage.SessionId) },
                { "foreignField", nameof(EngageChatSession.Id) },
                { "as", "session" }
            }),
            new BsonDocument("$unwind", "$session"),
            new BsonDocument("$match", new BsonDocument("session.TenantId", new BsonDocument("$in", new BsonArray(tenantIds.Select(item => new BsonBinaryData(item, GuidRepresentation.Standard)))))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$session.TenantId" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var grouped = await _engageMessages.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var item in grouped)
        {
            var id = item["_id"].AsGuid;
            result[id] = item["count"].ToInt32();
        }

        return result;
    }

    private static async Task<Dictionary<Guid, DateTime>> MaxDateByTenantAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        IReadOnlyCollection<Guid> tenantIds,
        string tenantField,
        string dateField,
        CancellationToken cancellationToken)
    {
        var filter = Builders<TDocument>.Filter.In(tenantField, tenantIds);

        var grouped = await collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", "$" + tenantField },
                { "value", new BsonDocument("$max", "$" + dateField) }
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, DateTime>();
        foreach (var item in grouped)
        {
            if (!item.TryGetValue("value", out var value) || value.IsBsonNull)
            {
                continue;
            }

            result[item["_id"].AsGuid] = value.ToUniversalTime();
        }

        return result;
    }

    private static int GetCount(IReadOnlyDictionary<Guid, int> source, Guid tenantId)
        => source.TryGetValue(tenantId, out var value) ? value : 0;

    private static DateTime? GetDate(IReadOnlyDictionary<Guid, DateTime> source, Guid tenantId)
        => source.TryGetValue(tenantId, out var value) ? value : null;

    private static DateTime? Max(params DateTime?[] values)
    {
        var normalized = values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
        return normalized.Length == 0 ? null : normalized.Max();
    }

    private static PlatformTenantUsageResult EmptyUsage()
        => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
}
