using System.Security.Claims;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Visitors.Api;

internal static class VisitorsEndpoints
{
    // ── GET /visitors ──────────────────────────────────────────────────────

    public static async Task<IResult> ListVisitorsAsync(
        HttpContext context,
        string siteId,
        int page,
        int pageSize,
        ListVisitorsHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
            return BadRequest("siteId", "Site id is invalid.");

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        page     = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var result = await handler.HandleAsync(
            new ListVisitorsQuery(tenantId.Value, siteGuid, page, pageSize),
            context.RequestAborted);

        return Results.Ok(result);
    }

    // ── GET /visitors/{visitorId}/timeline ────────────────────────────────

    public static async Task<IResult> GetTimelineAsync(
        HttpContext context,
        string visitorId,
        string siteId,
        int limit,
        GetVisitorTimelineHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))    errors["siteId"]    = ["Site id is invalid."];
        if (!Guid.TryParse(visitorId, out var visitorGuid)) errors["visitorId"] = ["Visitor id is invalid."];
        if (errors.Count > 0) return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        limit = limit is <= 0 or > 500 ? 200 : limit;

        var result = await handler.HandleAsync(
            new VisitorTimelineQuery(tenantId.Value, siteGuid, visitorGuid, limit),
            context.RequestAborted);

        return Results.Ok(result);
    }

    // ── GET /visitors/{visitorId} ─────────────────────────────────────────

    public static async Task<IResult> GetVisitorAsync(
        HttpContext context,
        string visitorId,
        string siteId,
        GetVisitorDetailHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))    errors["siteId"]    = ["Site id is invalid."];
        if (!Guid.TryParse(visitorId, out var visitorGuid)) errors["visitorId"] = ["Visitor id is invalid."];
        if (errors.Count > 0) return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(
            new GetVisitorDetailQuery(tenantId.Value, siteGuid, visitorGuid),
            context.RequestAborted);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    // ── GET /visitors/visits/counts ───────────────────────────────────────

    public static async Task<IResult> GetVisitCountsAsync(
        HttpContext context,
        string siteId,
        GetVisitCountWindowsHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(tenantId.Value, siteGuid, context.RequestAborted);
        return Results.Ok(new VisitCountsResponse(result.Last7, result.Last30, result.Last90));
    }

    // ── GET /visitors/online-now ──────────────────────────────────────────
    // Returns live visitors — who is on the site right now.
    // windowMinutes defaults to 5, limit defaults to 20.

    public static async Task<IResult> GetOnlineNowAsync(
        HttpContext context,
        string siteId,
        int windowMinutes,
        int limit,
        GetOnlineNowHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var normalizedWindow = windowMinutes is <= 0 or > 120 ? 5 : windowMinutes;
        var normalizedLimit  = limit is <= 0 or > 200 ? 20 : limit;

        var visitors = await handler.HandleAsync(
            new OnlineNowQuery(tenantId.Value, siteGuid, normalizedWindow, normalizedLimit),
            context.RequestAborted);

        return Results.Ok(new OnlineNowResponse(
            normalizedWindow,
            visitors.Count,
            visitors.Select(v => new OnlineVisitorResponse(
                v.VisitorId.ToString("N"),
                v.LastSeenAtUtc,
                v.ActiveSessionsCount,
                v.LastPath,
                v.LastReferrer,
                v.Country,
                v.Platform,
                v.PrimaryEmail)).ToArray()));
    }

    // ── GET /visitors/analytics/pages ─────────────────────────────────────

    public static async Task<IResult> GetPageAnalyticsAsync(
        HttpContext context,
        string siteId,
        int days,
        int limit,
        GetPageAnalyticsHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var normalizedDays = days is <= 0 or > 90 ? 7 : days;

        var pages = await handler.HandleAsync(
            new PageAnalyticsQuery(tenantId.Value, siteGuid, normalizedDays, limit),
            context.RequestAborted);

        return Results.Ok(new PageAnalyticsResponse(
            normalizedDays,
            pages.Select(p => new PageAnalyticsItemResponse(
                p.PageUrl, p.PageViews, p.UniqueSessions, p.AvgTimeOnPageSeconds)).ToArray()));
    }

    // ── GET /visitors/analytics/countries ─────────────────────────────────
    // Phase 2: New endpoint — visitor breakdown by country.

    public static async Task<IResult> GetCountryBreakdownAsync(
        HttpContext context,
        string siteId,
        int days,
        int limit,
        GetCountryBreakdownHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var normalizedDays = days is <= 0 or > 90 ? 7 : days;
        var normalizedLimit = limit is <= 0 or > 100 ? 20 : limit;

        var countries = await handler.HandleAsync(
            new CountryBreakdownQuery(tenantId.Value, siteGuid, normalizedDays, normalizedLimit),
            context.RequestAborted);

        return Results.Ok(new CountryBreakdownResponse(normalizedDays, countries.ToArray()));
    }

    // GET /visitors/analytics/dashboard?siteId=
    // Returns all Phase 3 dashboard metrics in one call.
    
    public static async Task<IResult> GetDashboardAnalyticsAsync(
        HttpContext context,
        string siteId,
        GetDashboardAnalyticsHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
                new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] }));
    
        var tenantId = context.User.FindFirstValue("tenantId");
        if (!Guid.TryParse(tenantId, out var tenantGuid)) return Results.Unauthorized();
    
        var result = await handler.HandleAsync(new DashboardAnalyticsQuery(tenantGuid, siteGuid), context.RequestAborted);
        return Results.Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var v = user.FindFirstValue("tenantId");
        return Guid.TryParse(v, out var id) ? id : null;
    }

    private static IResult BadRequest(string field, string message) =>
        Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = [message] }));
}
