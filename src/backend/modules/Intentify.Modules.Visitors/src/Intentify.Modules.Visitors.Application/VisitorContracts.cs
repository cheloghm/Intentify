using Intentify.Modules.Collector.Application;
using Intentify.Modules.Visitors.Domain;

namespace Intentify.Modules.Visitors.Application;

// ── Collector event ingestion ─────────────────────────────────────────────────

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
    string? Platform,
    string? Country = null,
    string? City    = null);

public sealed record UpsertVisitorResult(Guid VisitorId, VisitorSession Session, int SessionsCount = 0);

// ── Visitor list ──────────────────────────────────────────────────────────────

public sealed record ListVisitorsQuery(Guid TenantId, Guid SiteId, int Page, int PageSize);

public sealed record VisitorListItem(
    Guid VisitorId,
    DateTime LastSeenAtUtc,
    int SessionsCount,
    int TotalPagesVisited,
    int LastSessionEngagementScore,
    string? LastPath,
    string? LastReferrer,
    string? Country      = null,
    string? Platform     = null,
    string? PrimaryEmail = null);

// ── Timeline ──────────────────────────────────────────────────────────────────

public sealed record VisitorTimelineQuery(Guid TenantId, Guid SiteId, Guid VisitorId, int Limit);

public sealed record VisitorTimelineItem(
    DateTime OccurredAtUtc,
    string Type,
    string? SessionId,
    string Url,
    string? Referrer,
    IReadOnlyDictionary<string, string>? MetadataSummary);

// ── Visitor detail ────────────────────────────────────────────────────────────

public sealed record GetVisitorDetailQuery(Guid TenantId, Guid SiteId, Guid VisitorId);

public sealed record VisitorRecentSessionItem(
    string SessionId,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    int PagesVisited,
    int TimeOnSiteSeconds,
    int EngagementScore,
    string? LastPath,
    string? LastReferrer,
    IReadOnlyDictionary<string, int> TopActions);

public sealed record VisitorIdentificationSummary(
    bool IsIdentified,
    DateTime? LastIdentifiedAtUtc,
    string Source,
    decimal Confidence,
    IReadOnlyCollection<string> KnownTraits,
    string? Context);

public sealed record VisitorDetailResult(
    Guid VisitorId,
    Guid SiteId,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    int VisitCount,
    int TotalPagesVisited,
    string? PrimaryEmail,
    string? DisplayName,
    string? Phone,
    string? UserAgent,
    string? Language,
    string? Platform,
    string? Country,
    VisitorIdentificationSummary Identification,
    IReadOnlyCollection<VisitorRecentSessionItem> RecentSessions);

// ── Visit counts ──────────────────────────────────────────────────────────────

public sealed record VisitCountWindows(int Last7, int Last30, int Last90);

// ── Online now ────────────────────────────────────────────────────────────────

public sealed record OnlineNowQuery(Guid TenantId, Guid SiteId, int WindowMinutes, int Limit);

public sealed record OnlineVisitorItem(
    Guid VisitorId,
    DateTime LastSeenAtUtc,
    int ActiveSessionsCount,
    string? LastPath,
    string? LastReferrer,
    string? Country      = null,
    string? Platform     = null,
    string? PrimaryEmail = null);

// ── Page analytics ────────────────────────────────────────────────────────────

public sealed record PageAnalyticsQuery(Guid TenantId, Guid SiteId, int Days, int Limit);

public sealed record PageAnalyticsItem(
    string PageUrl,
    int PageViews,
    int UniqueSessions,
    decimal AvgTimeOnPageSeconds);

// ── Country breakdown ─────────────────────────────────────────────────────────

public sealed record CountryBreakdownQuery(Guid TenantId, Guid SiteId, int Days, int Limit);

public sealed record CountryBreakdownItem(string Country, int VisitorCount, double Percentage);

// ── Consent ───────────────────────────────────────────────────────────────────

public sealed record RecordVisitorConsentCommand(
    Guid TenantId,
    Guid SiteId,
    Guid VisitorId,
    bool ConsentGiven,
    string Version = "1.0");

public sealed record RecordVisitorConsentResult(bool Recorded);

// ── Interfaces ────────────────────────────────────────────────────────────────

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

public interface IVisitorConsentWriter
{
    Task<RecordVisitorConsentResult> RecordConsentAsync(RecordVisitorConsentCommand command, CancellationToken cancellationToken = default);
}

public interface IVisitorAnalyticsReader
{
    Task<IReadOnlyCollection<OnlineVisitorItem>> GetOnlineNowAsync(Guid tenantId, Guid siteId, DateTime cutoffUtc, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PageAnalyticsItem>> GetTopPagesAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CountryBreakdownItem>> GetCountryBreakdownAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, int limit, CancellationToken cancellationToken = default);
}

// ── Notifications & observers ─────────────────────────────────────────────────

public sealed record VisitorPageViewNotification(
    Guid TenantId,
    Guid SiteId,
    Guid VisitorId,
    string? SessionId,
    string? PageUrl,
    int SessionsCount,
    DateTime OccurredAtUtc);

public interface IVisitorEventObserver
{
    Task OnPageViewAsync(VisitorPageViewNotification notification, CancellationToken ct = default);
}

public sealed class CollectorVisitorEventObserver : ICollectorEventObserver
{
    private readonly UpsertVisitorFromCollectorEventHandler _handler;
    private readonly IReadOnlyCollection<IVisitorEventObserver> _visitorObservers;

    public CollectorVisitorEventObserver(UpsertVisitorFromCollectorEventHandler handler, IEnumerable<IVisitorEventObserver> visitorObservers)
    {
        _handler = handler;
        _visitorObservers = visitorObservers.ToArray();
    }

    public async Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default)
    {
        var result = await _handler.HandleAsync(new UpsertVisitorFromCollectorEvent(
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

        if (_visitorObservers.Count > 0
            && string.Equals(notification.Type, "pageview", StringComparison.OrdinalIgnoreCase))
        {
            var pageViewNotification = new VisitorPageViewNotification(
                notification.TenantId,
                notification.SiteId,
                result.VisitorId,
                notification.SessionId,
                notification.Url,
                result.SessionsCount,
                notification.OccurredAtUtc);

            foreach (var observer in _visitorObservers)
                await observer.OnPageViewAsync(pageViewNotification, cancellationToken);
        }
    }
}
