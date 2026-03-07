namespace Intentify.Modules.PlatformAdmin.Application;

public sealed record PlatformSummaryResult(
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

public sealed record PlatformTenantUsageResult(
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

public sealed record PlatformTenantListRowResult(
    Guid TenantId,
    string TenantName,
    string Domain,
    string Plan,
    string Industry,
    string Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlatformTenantUsageResult Usage);

public sealed record PlatformTenantListResult(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<PlatformTenantListRowResult> Items);

public sealed record PlatformTenantSiteResult(
    Guid SiteId,
    string Domain,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FirstEventReceivedAtUtc);

public sealed record PlatformTenantRecentActivityResult(
    DateTime? LastSiteActivityAtUtc,
    DateTime? LastVisitorActivityAtUtc,
    DateTime? LastEngageSessionActivityAtUtc,
    DateTime? LastTicketActivityAtUtc,
    DateTime? LastPromoActivityAtUtc,
    DateTime? LastPromoEntryActivityAtUtc,
    DateTime? LastIntelligenceActivityAtUtc,
    DateTime? LastAdsActivityAtUtc,
    DateTime? LastKnowledgeActivityAtUtc);

public sealed record PlatformTenantDetailResult(
    Guid TenantId,
    string TenantName,
    string Domain,
    string Plan,
    string Industry,
    string Category,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    PlatformTenantUsageResult Usage,
    PlatformTenantRecentActivityResult RecentActivity,
    IReadOnlyCollection<PlatformTenantSiteResult> Sites);

public sealed record PlatformOperationalSummaryResult(
    string HealthStatus,
    int TotalKnowledgeSources,
    int IndexedKnowledgeSources,
    int FailedKnowledgeSources,
    int QueuedKnowledgeSources,
    int ProcessingKnowledgeSources,
    bool OpenSearchEnabled,
    bool OpenSearchConfigured,
    DateTime GeneratedAtUtc);

public sealed record ListPlatformTenantsQuery(int Page, int PageSize, string? Search);

public interface IPlatformAdminReadRepository
{
    Task<PlatformSummaryResult> GetPlatformSummaryAsync(CancellationToken cancellationToken = default);
    Task<PlatformTenantListResult> ListTenantsAsync(ListPlatformTenantsQuery query, CancellationToken cancellationToken = default);
    Task<PlatformTenantDetailResult?> GetTenantDetailAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PlatformOperationalSummaryResult> GetOperationalSummaryAsync(CancellationToken cancellationToken = default);
}
