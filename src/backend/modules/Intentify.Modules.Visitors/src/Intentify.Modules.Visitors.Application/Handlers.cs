namespace Intentify.Modules.Visitors.Application;

public sealed class UpsertVisitorFromCollectorEventHandler(IVisitorRepository repository)
{
    public Task<UpsertVisitorResult> HandleAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default)
        => repository.UpsertFromCollectorEventAsync(command, cancellationToken);
}

public sealed class ListVisitorsHandler(IVisitorRepository repository)
{
    public Task<IReadOnlyCollection<VisitorListItem>> HandleAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default)
        => repository.ListAsync(query, cancellationToken);
}

public sealed class GetVisitorTimelineHandler(
    IVisitorRepository repository,
    IVisitorTimelineReader timelineReader,
    VisitorsRetentionOptions retentionOptions)
{
    public async Task<IReadOnlyCollection<VisitorTimelineItem>> HandleAsync(VisitorTimelineQuery query, CancellationToken cancellationToken = default)
    {
        var visitor = await repository.GetByIdAsync(query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
        if (visitor is null) return [];

        DateTime? floor = retentionOptions.RetentionDays is > 0
            ? DateTime.UtcNow.AddDays(-retentionOptions.RetentionDays.Value)
            : null;

        var sessionIds = visitor.Sessions.Select(s => s.SessionId).Distinct(StringComparer.Ordinal).ToArray();
        return await timelineReader.GetTimelineAsync(query, sessionIds, floor, cancellationToken);
    }
}

public sealed class GetVisitCountWindowsHandler(
    IVisitorRepository repository,
    VisitorsRetentionOptions retentionOptions)
{
    public async Task<VisitCountWindows> HandleAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        DateTime? floor = retentionOptions.RetentionDays is > 0 ? now.AddDays(-retentionOptions.RetentionDays.Value) : null;

        var s7  = Max(now.AddDays(-7),  floor);
        var s30 = Max(now.AddDays(-30), floor);
        var s90 = Max(now.AddDays(-90), floor);

        var last7  = await repository.CountSessionsSinceAsync(tenantId, siteId, s7,  floor, cancellationToken);
        var last30 = await repository.CountSessionsSinceAsync(tenantId, siteId, s30, floor, cancellationToken);
        var last90 = await repository.CountSessionsSinceAsync(tenantId, siteId, s90, floor, cancellationToken);

        return new VisitCountWindows(last7, last30, last90);
    }

    private static DateTime Max(DateTime a, DateTime? b) => b.HasValue && b.Value > a ? b.Value : a;
}

public sealed class GetVisitorDetailHandler(IVisitorRepository repository)
{
    private const int DefaultRecentSessionCount = 5;

    public async Task<VisitorDetailResult?> HandleAsync(GetVisitorDetailQuery query, CancellationToken cancellationToken = default)
    {
        var visitor = await repository.GetByIdAsync(query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
        if (visitor is null) return null;

        var firstSeen = visitor.Sessions.Count > 0
            ? visitor.Sessions.Min(s => s.FirstSeenAtUtc)
            : visitor.CreatedAtUtc;

        var recentSessions = visitor.Sessions
            .OrderByDescending(s => s.LastSeenAtUtc)
            .Take(DefaultRecentSessionCount)
            .Select(s => new VisitorRecentSessionItem(
                s.SessionId, s.FirstSeenAtUtc, s.LastSeenAtUtc,
                s.PagesVisited, s.TimeOnSiteSeconds, s.EngagementScore,
                s.LastPath, s.LastReferrer,
                new Dictionary<string, int>(s.TopActions, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return new VisitorDetailResult(
            visitor.Id, visitor.SiteId, firstSeen, visitor.LastSeenAtUtc,
            visitor.Sessions.Count, visitor.Sessions.Sum(s => s.PagesVisited),
            visitor.PrimaryEmail, visitor.DisplayName, visitor.Phone,
            visitor.UserAgentHint, visitor.Language, visitor.Platform,
            visitor.Country,
            BuildIdentification(visitor),
            recentSessions,
            IntentScore: visitor.IntentScore,
            CompanyName: visitor.CompanyName,
            CompanyDomain: visitor.CompanyDomain,
            CompanyIndustry: visitor.CompanyIndustry,
            CompanySize: visitor.CompanySize);
    }

    private static VisitorIdentificationSummary BuildIdentification(Domain.Visitor visitor)
    {
        var traits = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(visitor.PrimaryEmail)) traits.Add("email");
        if (!string.IsNullOrWhiteSpace(visitor.DisplayName))  traits.Add("name");
        if (!string.IsNullOrWhiteSpace(visitor.Phone))        traits.Add("phone");

        return new VisitorIdentificationSummary(
            traits.Count > 0,
            visitor.LastIdentifiedAtUtc,
            visitor.LastIdentifiedAtUtc.HasValue ? "lead_capture" : "unknown",
            traits.Count / 3m,
            traits,
            traits.Count > 0 ? "Identity enriched through consented lead-linking." : null);
    }
}

public sealed class GetOnlineNowHandler(IVisitorAnalyticsReader analyticsReader)
{
    public Task<IReadOnlyCollection<OnlineVisitorItem>> HandleAsync(OnlineNowQuery query, CancellationToken cancellationToken = default)
    {
        var window = query.WindowMinutes is <= 0 or > 120 ? 5 : query.WindowMinutes;
        var limit  = query.Limit is <= 0 or > 200 ? 20 : query.Limit;
        var cutoff = DateTime.UtcNow.AddMinutes(-window);
        return analyticsReader.GetOnlineNowAsync(query.TenantId, query.SiteId, cutoff, limit, cancellationToken);
    }
}

public sealed class GetPageAnalyticsHandler(IVisitorAnalyticsReader analyticsReader)
{
    public Task<IReadOnlyCollection<PageAnalyticsItem>> HandleAsync(PageAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        var days  = query.Days is <= 0 or > 90 ? 7 : query.Days;
        var limit = query.Limit is <= 0 or > 100 ? 10 : query.Limit;
        return analyticsReader.GetTopPagesAsync(query.TenantId, query.SiteId, DateTime.UtcNow.AddDays(-days), limit, cancellationToken);
    }
}

// ── Phase 2: Country breakdown handler ───────────────────────────────────────

public sealed class GetCountryBreakdownHandler(IVisitorAnalyticsReader analyticsReader)
{
    public Task<IReadOnlyCollection<CountryBreakdownItem>> HandleAsync(CountryBreakdownQuery query, CancellationToken cancellationToken = default)
    {
        var days  = query.Days is <= 0 or > 90 ? 7 : query.Days;
        var limit = query.Limit is <= 0 or > 100 ? 20 : query.Limit;
        return analyticsReader.GetCountryBreakdownAsync(query.TenantId, query.SiteId, DateTime.UtcNow.AddDays(-days), limit, cancellationToken);
    }
}
