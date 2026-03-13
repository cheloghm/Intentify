using System.Security.Claims;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Visitors.Api;

internal static class VisitorsEndpoints
{
    public static async Task<IResult> ListVisitorsAsync(
        HttpContext context,
        string siteId,
        int page,
        int pageSize,
        ListVisitorsHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var result = await handler.HandleAsync(
            new ListVisitorsQuery(tenantId.Value, siteGuid, page, pageSize),
            context.RequestAborted);

        return Results.Ok(result);
    }

    public static async Task<IResult> GetTimelineAsync(
        HttpContext context,
        string visitorId,
        string siteId,
        int limit,
        GetVisitorTimelineHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (!Guid.TryParse(visitorId, out var parsedVisitorId))
        {
            errors["visitorId"] = ["Visitor id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        limit = limit is <= 0 or > 500 ? 200 : limit;

        var result = await handler.HandleAsync(
            new VisitorTimelineQuery(tenantId.Value, siteGuid, parsedVisitorId, limit),
            context.RequestAborted);

        return Results.Ok(result);
    }

    public static async Task<IResult> GetVisitorAsync(
        HttpContext context,
        string visitorId,
        string siteId,
        GetVisitorDetailHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (!Guid.TryParse(visitorId, out var parsedVisitorId))
        {
            errors["visitorId"] = ["Visitor id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(
            new GetVisitorDetailQuery(tenantId.Value, siteGuid, parsedVisitorId),
            context.RequestAborted);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    public static async Task<IResult> GetVisitCountsAsync(
        HttpContext context,
        string siteId,
        GetVisitCountWindowsHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(tenantId.Value, siteGuid, context.RequestAborted);
        return Results.Ok(new VisitCountsResponse(result.Last7, result.Last30, result.Last90));
    }

    public static async Task<IResult> GetOnlineNowAsync(
        HttpContext context,
        string siteId,
        int windowMinutes,
        int limit,
        GetOnlineNowHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var visitors = await handler.HandleAsync(new OnlineNowQuery(tenantId.Value, siteGuid, windowMinutes, limit), context.RequestAborted);
        var normalizedWindowMinutes = windowMinutes is <= 0 or > 120 ? 5 : windowMinutes;

        return Results.Ok(new OnlineNowResponse(
            normalizedWindowMinutes,
            visitors.Count,
            visitors.Select(item => new OnlineVisitorResponse(
                item.VisitorId.ToString("N"),
                item.LastSeenAtUtc,
                item.ActiveSessionsCount,
                item.LastPath,
                item.LastReferrer)).ToArray()));
    }

    public static async Task<IResult> GetPageAnalyticsAsync(
        HttpContext context,
        string siteId,
        int days,
        int limit,
        GetPageAnalyticsHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var pages = await handler.HandleAsync(new PageAnalyticsQuery(tenantId.Value, siteGuid, days, limit), context.RequestAborted);
        var normalizedDays = days is <= 0 or > 90 ? 7 : days;

        return Results.Ok(new PageAnalyticsResponse(
            normalizedDays,
            pages.Select(item => new PageAnalyticsItemResponse(
                item.PageUrl,
                item.PageViews,
                item.UniqueSessions,
                item.AvgTimeOnPageSeconds)).ToArray()));
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
