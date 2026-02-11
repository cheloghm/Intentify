using Intentify.Modules.Collector.Application;
using Intentify.Modules.Visitors.Domain;

namespace Intentify.Modules.Visitors.Application;

public sealed record UpsertVisitorFromCollectorEvent(
    Guid SiteId,
    Guid TenantId,
    DateTime OccurredAtUtc,
    string EventType,
    string? SessionId,
    string? Url,
    string? Referrer,
    string? FirstPartyId,
    string? UserAgent,
    string? Language,
    string? Platform);

public sealed record UpsertVisitorResult(Guid VisitorId, VisitorSession Session);

public sealed record ListVisitorsQuery(Guid TenantId, Guid SiteId, int Page, int PageSize);

public sealed record VisitorListItem(
    Guid VisitorId,
    DateTime LastSeenAtUtc,
    int SessionsCount,
    int TotalPagesVisited,
    int LastSessionEngagementScore,
    string? LastPath,
    string? LastReferrer);

public sealed record VisitorTimelineQuery(Guid TenantId, Guid SiteId, Guid VisitorId, int Limit);

public sealed record VisitorTimelineItem(
    DateTime OccurredAtUtc,
    string Type,
    string Url,
    string? Referrer,
    IReadOnlyDictionary<string, string>? MetadataSummary);

public sealed record VisitCountWindows(int Last7, int Last30, int Last90);

public interface IVisitorRepository
{
    Task<UpsertVisitorResult> UpsertFromCollectorEventAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<VisitorListItem>> ListAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default);
    Task<Visitor?> GetByIdAsync(Guid tenantId, Guid siteId, Guid visitorId, CancellationToken cancellationToken = default);
    Task<int> CountSessionsSinceAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default);
}

public interface IVisitorTimelineReader
{
    Task<IReadOnlyCollection<VisitorTimelineItem>> GetTimelineAsync(VisitorTimelineQuery query, IReadOnlyCollection<string> sessionIds, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default);
}

public sealed class CollectorVisitorEventObserver : ICollectorEventObserver
{
    private readonly UpsertVisitorFromCollectorEventHandler _handler;

    public CollectorVisitorEventObserver(UpsertVisitorFromCollectorEventHandler handler)
    {
        _handler = handler;
    }

    public Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default)
    {
        return _handler.HandleAsync(new UpsertVisitorFromCollectorEvent(
            notification.SiteId,
            notification.TenantId,
            notification.OccurredAtUtc,
            notification.Type,
            notification.SessionId,
            notification.Url,
            notification.Referrer,
            notification.FirstPartyId,
            notification.UserAgent,
            notification.Language,
            notification.Platform), cancellationToken);
    }
}
