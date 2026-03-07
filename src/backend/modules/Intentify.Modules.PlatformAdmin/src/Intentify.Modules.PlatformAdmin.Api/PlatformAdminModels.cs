namespace Intentify.Modules.PlatformAdmin.Api;

public sealed record PlatformSummaryResponse(
    int TotalTenants,
    int TotalSites,
    int TotalVisitors,
    int TotalEngageSessions,
    int TotalEngageMessages,
    int TotalTickets,
    int TotalPromos,
    int TotalPromoEntries,
    int TotalIntelligenceTrendRecords,
    int TotalKnowledgeSources,
    int IndexedKnowledgeSources,
    int FailedKnowledgeSources,
    string HealthStatus,
    DateTime GeneratedAtUtc);

public sealed record PlatformTenantUsageResponse(
    int SiteCount,
    int VisitorsCount,
    int EngageSessionsCount,
    int EngageMessagesCount,
    int TicketsCount,
    int PromosCount,
    int PromoEntriesCount,
    int IntelligenceRecordCount,
    int AdsCampaignCount,
    int KnowledgeSourcesCount,
    int KnowledgeIndexedCount,
    int KnowledgeFailedCount,
    DateTime? LastActivityAtUtc);

public sealed record PlatformTenantListRowResponse(
    string TenantId,
    string TenantName,
    string Domain,
    string Plan,
    string Industry,
    string Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlatformTenantUsageResponse Usage);

public sealed record PlatformTenantListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<PlatformTenantListRowResponse> Items);

public sealed record PlatformTenantSiteResponse(
    string SiteId,
    string Domain,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FirstEventReceivedAtUtc);

public sealed record PlatformTenantRecentActivityResponse(
    DateTime? LastSiteActivityAtUtc,
    DateTime? LastVisitorActivityAtUtc,
    DateTime? LastEngageSessionActivityAtUtc,
    DateTime? LastTicketActivityAtUtc,
    DateTime? LastPromoActivityAtUtc,
    DateTime? LastPromoEntryActivityAtUtc,
    DateTime? LastIntelligenceActivityAtUtc,
    DateTime? LastAdsActivityAtUtc,
    DateTime? LastKnowledgeActivityAtUtc);

public sealed record PlatformTenantDetailResponse(
    string TenantId,
    string TenantName,
    string Domain,
    string Plan,
    string Industry,
    string Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlatformTenantUsageResponse Usage,
    PlatformTenantRecentActivityResponse RecentActivity,
    IReadOnlyCollection<PlatformTenantSiteResponse> Sites);

public sealed record PlatformOperationalSummaryResponse(
    string HealthStatus,
    int TotalKnowledgeSources,
    int IndexedKnowledgeSources,
    int FailedKnowledgeSources,
    int QueuedKnowledgeSources,
    int ProcessingKnowledgeSources,
    bool OpenSearchEnabled,
    bool OpenSearchConfigured,
    DateTime GeneratedAtUtc);
