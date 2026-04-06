namespace Intentify.Modules.Visitors.Api;

public sealed record VisitCountsResponse(int Last7, int Last30, int Last90);

public sealed record OnlineNowResponse(
    int WindowMinutes,
    int Count,
    IReadOnlyCollection<OnlineVisitorResponse> Visitors);

public sealed record OnlineVisitorResponse(
    string VisitorId,
    DateTime LastSeenAtUtc,
    int ActiveSessionsCount,
    string? LastPath,
    string? LastReferrer,
    string? Country      = null,
    string? Platform     = null,
    string? PrimaryEmail = null);

public sealed record PageAnalyticsResponse(
    int Days,
    IReadOnlyCollection<PageAnalyticsItemResponse> Pages);

public sealed record PageAnalyticsItemResponse(
    string PageUrl,
    int PageViews,
    int UniqueSessions,
    decimal AvgTimeOnPageSeconds);

public sealed record CountryBreakdownResponse(
    int Days,
    IReadOnlyCollection<Application.CountryBreakdownItem> Countries);
