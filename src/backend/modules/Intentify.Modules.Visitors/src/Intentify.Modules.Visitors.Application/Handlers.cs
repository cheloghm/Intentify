namespace Intentify.Modules.Visitors.Application;

public sealed class UpsertVisitorFromCollectorEventHandler
{
    private readonly IVisitorRepository _repository;

    public UpsertVisitorFromCollectorEventHandler(IVisitorRepository repository)
    {
        _repository = repository;
    }

    public Task<UpsertVisitorResult> HandleAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default)
    {
        return _repository.UpsertFromCollectorEventAsync(command, cancellationToken);
    }
}

public sealed class ListVisitorsHandler
{
    private readonly IVisitorRepository _repository;

    public ListVisitorsHandler(IVisitorRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<VisitorListItem>> HandleAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(query, cancellationToken);
    }
}

public sealed class GetVisitorTimelineHandler
{
    private readonly IVisitorRepository _repository;
    private readonly IVisitorTimelineReader _timelineReader;
    private readonly VisitorsRetentionOptions _retentionOptions;

    public GetVisitorTimelineHandler(IVisitorRepository repository, IVisitorTimelineReader timelineReader, VisitorsRetentionOptions retentionOptions)
    {
        _repository = repository;
        _timelineReader = timelineReader;
        _retentionOptions = retentionOptions;
    }

    public async Task<IReadOnlyCollection<VisitorTimelineItem>> HandleAsync(VisitorTimelineQuery query, CancellationToken cancellationToken = default)
    {
        var visitor = await _repository.GetByIdAsync(query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
        if (visitor is null)
        {
            return Array.Empty<VisitorTimelineItem>();
        }

        DateTime? retentionFloorUtc = _retentionOptions.RetentionDays is > 0
            ? DateTime.UtcNow.AddDays(-_retentionOptions.RetentionDays.Value)
            : null;

        var sessionIds = visitor.Sessions.Select(session => session.SessionId).Distinct(StringComparer.Ordinal).ToArray();
        return await _timelineReader.GetTimelineAsync(query, sessionIds, retentionFloorUtc, cancellationToken);
    }
}

public sealed class GetVisitCountWindowsHandler
{
    private readonly IVisitorRepository _repository;
    private readonly VisitorsRetentionOptions _retentionOptions;

    public GetVisitCountWindowsHandler(IVisitorRepository repository, VisitorsRetentionOptions retentionOptions)
    {
        _repository = repository;
        _retentionOptions = retentionOptions;
    }

    public async Task<VisitCountWindows> HandleAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        DateTime? retentionFloorUtc = _retentionOptions.RetentionDays is > 0
            ? now.AddDays(-_retentionOptions.RetentionDays.Value)
            : null;

        var last7SinceUtc = now.AddDays(-7);
        var last30SinceUtc = now.AddDays(-30);
        var last90SinceUtc = now.AddDays(-90);

        if (retentionFloorUtc is { } floor)
        {
            last7SinceUtc = Max(last7SinceUtc, floor);
            last30SinceUtc = Max(last30SinceUtc, floor);
            last90SinceUtc = Max(last90SinceUtc, floor);
        }

        var last7 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last7SinceUtc, retentionFloorUtc, cancellationToken);
        var last30 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last30SinceUtc, retentionFloorUtc, cancellationToken);
        var last90 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last90SinceUtc, retentionFloorUtc, cancellationToken);

        return new VisitCountWindows(last7, last30, last90);
    }

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;
}

public sealed class GetVisitorDetailHandler
{
    private const int DefaultRecentSessionCount = 5;
    private readonly IVisitorRepository _repository;

    public GetVisitorDetailHandler(IVisitorRepository repository)
    {
        _repository = repository;
    }

    public async Task<VisitorDetailResult?> HandleAsync(GetVisitorDetailQuery query, CancellationToken cancellationToken = default)
    {
        var visitor = await _repository.GetByIdAsync(query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
        if (visitor is null)
        {
            return null;
        }

        var firstSeenAtUtc = visitor.Sessions.Count > 0
            ? visitor.Sessions.Min(session => session.FirstSeenAtUtc)
            : visitor.CreatedAtUtc;

        var recentSessions = visitor.Sessions
            .OrderByDescending(session => session.LastSeenAtUtc)
            .Take(DefaultRecentSessionCount)
            .Select(session => new VisitorRecentSessionItem(
                session.SessionId,
                session.FirstSeenAtUtc,
                session.LastSeenAtUtc,
                session.PagesVisited,
                session.TimeOnSiteSeconds,
                session.EngagementScore,
                session.LastPath,
                session.LastReferrer,
                new Dictionary<string, int>(session.TopActions, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return new VisitorDetailResult(
            visitor.Id,
            visitor.SiteId,
            firstSeenAtUtc,
            visitor.LastSeenAtUtc,
            visitor.Sessions.Count,
            visitor.Sessions.Sum(session => session.PagesVisited),
            visitor.PrimaryEmail,
            visitor.DisplayName,
            visitor.Phone,
            visitor.UserAgentHint,
            visitor.Language,
            visitor.Platform,
            BuildIdentificationSummary(visitor),
            recentSessions);
    }

    private static VisitorIdentificationSummary BuildIdentificationSummary(Domain.Visitor visitor)
    {
        var knownTraits = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(visitor.PrimaryEmail)) knownTraits.Add("email");
        if (!string.IsNullOrWhiteSpace(visitor.DisplayName)) knownTraits.Add("name");
        if (!string.IsNullOrWhiteSpace(visitor.Phone)) knownTraits.Add("phone");

        var isIdentified = knownTraits.Count > 0;
        var source = visitor.LastIdentifiedAtUtc.HasValue
            ? "lead_capture"
            : "unknown";

        var confidence = knownTraits.Count / 3m;
        var context = isIdentified
            ? "Identity traits were enriched through existing consented lead-linking flow."
            : null;

        return new VisitorIdentificationSummary(
            isIdentified,
            visitor.LastIdentifiedAtUtc,
            source,
            confidence,
            knownTraits,
            context);
    }
}

public sealed class GetOnlineNowHandler
{
    private readonly IVisitorAnalyticsReader _analyticsReader;

    public GetOnlineNowHandler(IVisitorAnalyticsReader analyticsReader)
    {
        _analyticsReader = analyticsReader;
    }

    public Task<IReadOnlyCollection<OnlineVisitorItem>> HandleAsync(OnlineNowQuery query, CancellationToken cancellationToken = default)
    {
        var windowMinutes = query.WindowMinutes is <= 0 or > 120 ? 5 : query.WindowMinutes;
        var limit = query.Limit is <= 0 or > 200 ? 20 : query.Limit;
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-windowMinutes);
        return _analyticsReader.GetOnlineNowAsync(query.TenantId, query.SiteId, cutoffUtc, limit, cancellationToken);
    }
}

public sealed class GetPageAnalyticsHandler
{
    private readonly IVisitorAnalyticsReader _analyticsReader;

    public GetPageAnalyticsHandler(IVisitorAnalyticsReader analyticsReader)
    {
        _analyticsReader = analyticsReader;
    }

    public Task<IReadOnlyCollection<PageAnalyticsItem>> HandleAsync(PageAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        var days = query.Days is <= 0 or > 90 ? 7 : query.Days;
        var limit = query.Limit is <= 0 or > 100 ? 10 : query.Limit;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);
        return _analyticsReader.GetTopPagesAsync(query.TenantId, query.SiteId, sinceUtc, limit, cancellationToken);
    }
}

public sealed class GetOnlineNowHandler
{
    private readonly IVisitorAnalyticsReader _analyticsReader;

    public GetOnlineNowHandler(IVisitorAnalyticsReader analyticsReader)
    {
        _analyticsReader = analyticsReader;
    }

    public Task<IReadOnlyCollection<OnlineVisitorItem>> HandleAsync(OnlineNowQuery query, CancellationToken cancellationToken = default)
    {
        var windowMinutes = query.WindowMinutes is <= 0 or > 120 ? 5 : query.WindowMinutes;
        var limit = query.Limit is <= 0 or > 200 ? 20 : query.Limit;
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-windowMinutes);
        return _analyticsReader.GetOnlineNowAsync(query.TenantId, query.SiteId, cutoffUtc, limit, cancellationToken);
    }
}

public sealed class GetPageAnalyticsHandler
{
    private readonly IVisitorAnalyticsReader _analyticsReader;

    public GetPageAnalyticsHandler(IVisitorAnalyticsReader analyticsReader)
    {
        _analyticsReader = analyticsReader;
    }

    public Task<IReadOnlyCollection<PageAnalyticsItem>> HandleAsync(PageAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        var days = query.Days is <= 0 or > 90 ? 7 : query.Days;
        var limit = query.Limit is <= 0 or > 100 ? 10 : query.Limit;
        var sinceUtc = DateTime.UtcNow.AddDays(-days);
        return _analyticsReader.GetTopPagesAsync(query.TenantId, query.SiteId, sinceUtc, limit, cancellationToken);
    }
}
