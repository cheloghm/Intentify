using Intentify.Modules.Intelligence.Domain;

namespace Intentify.Modules.Intelligence.Application;

// ── Refresh ───────────────────────────────────────────────────────────────────

public sealed record RefreshIntelligenceRequest(
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    int? Limit,
    string? Keyword          = null,
    string? AgeRange         = null,
    int? CategoryId          = null,
    string? SearchType       = null,
    IReadOnlyList<string>? ComparisonTerms = null,
    string? SubRegion        = null);

public sealed record RefreshIntelligenceResult(
    string Provider,
    DateTime RefreshedAtUtc,
    int ItemsCount,
    int RelatedQueriesCount = 0,
    int RisingQueriesCount  = 0);

// ── Trend item responses ──────────────────────────────────────────────────────

public sealed record IntelligenceTrendItemResponse(
    string QueryOrTopic,
    double Score,
    int? Rank,
    bool IsRising = false);

public sealed record IntelligenceTrendsResponse(
    string Provider,
    string Category,
    string Location,
    string TimeWindow,
    DateTime RefreshedAtUtc,
    IReadOnlyList<IntelligenceTrendItemResponse> Items);

public sealed record IntelligenceStatusResponse(
    string Provider,
    string Category,
    string Location,
    string TimeWindow,
    DateTime RefreshedAtUtc,
    int ItemsCount);

// ── Dashboard query ───────────────────────────────────────────────────────────

/// <summary>
/// Full set of filter dimensions available on the Intelligence dashboard.
/// All fields except SiteId are optional — omit to use profile defaults.
/// </summary>
public sealed record IntelligenceDashboardQuery(
    Guid SiteId,
    string? Category,
    string? Location,
    string? TimeWindow,
    string? Provider,
    string? Keyword,
    string? AudienceType,
    int? Limit,
    string? AgeRange              = null,   // e.g. "18-24"
    int? CategoryId               = null,   // Google Trends taxonomy ID
    string? SearchType            = null,   // "web", "images", "news", "shopping", "youtube"
    string? ComparisonTerms       = null,   // comma-separated, up to 4
    string? SubRegion             = null);  // e.g. "GB-NIR" for Northern Ireland

// ── Dashboard response ────────────────────────────────────────────────────────

public sealed record IntelligenceDashboardSummaryResponse(
    int MatchingItemsCount,
    double AverageScore,
    double MaxScore,
    int RankedItemsCount,
    string? TopQueryOrTopic);

public sealed record IntelligenceDashboardTrendItemResponse(
    string QueryOrTopic,
    double Score,
    int? Rank,
    string Provider,
    bool IsRising    = false,
    string? Category = null);

public sealed record IntelligenceDashboardResponse(
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    string? AudienceType,
    string? Provider,
    string? AgeRange,
    string? SearchType,
    string? SubRegion,
    DateTime? RefreshedAtUtc,
    int TotalItems,
    IntelligenceDashboardSummaryResponse Summary,
    IReadOnlyList<IntelligenceDashboardTrendItemResponse> TopItems,
    IReadOnlyList<IntelligenceDashboardTrendItemResponse> RelatedQueries,
    IReadOnlyList<IntelligenceDashboardTrendItemResponse> RisingQueries);

// ── Profile ───────────────────────────────────────────────────────────────────

public sealed record UpsertIntelligenceProfileRequest(
    Guid SiteId,
    string ProfileName,
    string IndustryCategory,
    string PrimaryAudienceType,
    IReadOnlyCollection<string> TargetLocations,
    IReadOnlyCollection<string> PrimaryProductsOrServices,
    IReadOnlyCollection<string>? WatchTopics,
    IReadOnlyCollection<string>? SeasonalPriorities,
    bool IsActive,
    int? RefreshIntervalMinutes);

public sealed record IntelligenceProfileResponse(
    Guid SiteId,
    string ProfileName,
    string IndustryCategory,
    string PrimaryAudienceType,
    IReadOnlyCollection<string> TargetLocations,
    IReadOnlyCollection<string> PrimaryProductsOrServices,
    IReadOnlyCollection<string> WatchTopics,
    IReadOnlyCollection<string> SeasonalPriorities,
    bool IsActive,
    int? RefreshIntervalMinutes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

// ── Repository interfaces ─────────────────────────────────────────────────────

public interface IIntelligenceProfileRepository
{
    Task UpsertAsync(IntelligenceProfile profile, CancellationToken ct = default);
    Task<IntelligenceProfile?> GetAsync(string tenantId, Guid siteId, CancellationToken ct = default);
    Task<IReadOnlyList<IntelligenceProfile>> ListActiveAsync(CancellationToken ct = default);
}

public interface IIntelligenceTrendsRepository
{
    Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default);

    Task<IntelligenceTrendRecord?> GetAsync(
        string tenantId, Guid siteId,
        string category, string location, string timeWindow,
        CancellationToken ct = default);

    Task<IntelligenceStatusResponse?> GetStatusAsync(
        string tenantId, Guid siteId,
        string category, string location, string timeWindow,
        CancellationToken ct = default);
}
