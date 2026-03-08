using Intentify.Modules.Intelligence.Domain;

namespace Intentify.Modules.Intelligence.Application;

public sealed record RefreshIntelligenceRequest(Guid SiteId, string Category, string Location, string TimeWindow, int? Limit);

public sealed record RefreshIntelligenceResult(string Provider, DateTime RefreshedAtUtc, int ItemsCount);

public sealed record IntelligenceTrendItemResponse(string QueryOrTopic, double Score, int? Rank);

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

public sealed record IntelligenceDashboardQuery(
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    string? Provider,
    string? Keyword,
    string? AudienceType,
    int? Limit);

public sealed record IntelligenceDashboardSummaryResponse(int MatchingItemsCount, double AverageScore, double MaxScore);

public sealed record IntelligenceDashboardTrendItemResponse(string QueryOrTopic, double Score, int? Rank, string Provider);

public sealed record IntelligenceDashboardResponse(
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    string? AudienceType,
    string? Provider,
    DateTime? RefreshedAtUtc,
    int TotalItems,
    IntelligenceDashboardSummaryResponse Summary,
    IReadOnlyList<IntelligenceDashboardTrendItemResponse> TopItems);


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

public interface IIntelligenceProfileRepository
{
    Task UpsertAsync(IntelligenceProfile profile, CancellationToken ct = default);

    Task<IntelligenceProfile?> GetAsync(string tenantId, Guid siteId, CancellationToken ct = default);
}

public interface IIntelligenceTrendsRepository
{
    Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default);

    Task<IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default);

    Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default);
}
