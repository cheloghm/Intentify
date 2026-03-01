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

public interface IIntelligenceTrendsRepository
{
    Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default);

    Task<IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default);

    Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default);
}
