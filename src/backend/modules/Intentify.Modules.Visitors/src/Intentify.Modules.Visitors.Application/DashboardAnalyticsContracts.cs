namespace Intentify.Modules.Visitors.Application;

// ── Phase 3: Dashboard Analytics ──────────────────────────────────────────────

public sealed record DashboardAnalyticsQuery(Guid TenantId, Guid SiteId);

public sealed record DashboardAnalyticsResult(
    int TodayVisitors,
    int TodaySessions,
    int OnlineNow,
    int WeekVisitors,
    int LastWeekVisitors,
    double WeekChangePercent,
    double AvgSessionSeconds,
    double BounceRate,              // % of sessions with 1 page view
    IReadOnlyList<DailyVisitorPoint> Last14Days,
    IReadOnlyList<TopPageItem> TopPages,
    IReadOnlyList<CountryBreakdownItem> TopCountries);

public sealed record DailyVisitorPoint(string DateLabel, int Visitors, int Sessions);

public sealed record TopPageItem(string PageUrl, int PageViews, int UniqueSessions, double AvgTimeSeconds, double BounceRate);
