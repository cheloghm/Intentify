using System.Security.Claims;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Visitors.Api;

internal static class VisitorsEndpoints
{
    public static async Task<IResult> ListVisitorsAsync(
        HttpContext context,
        Guid siteId,
        int page,
        int pageSize,
        ListVisitorsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var result = await handler.HandleAsync(new ListVisitorsQuery(tenantId.Value, siteId, page, pageSize), context.RequestAborted);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetTimelineAsync(
        HttpContext context,
        string visitorId,
        Guid siteId,
        int limit,
        GetVisitorTimelineHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(visitorId, out var parsedVisitorId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["visitorId"] = ["Visitor id is invalid."]
            }));
        }

        limit = limit is <= 0 or > 500 ? 200 : limit;

        var result = await handler.HandleAsync(new VisitorTimelineQuery(tenantId.Value, siteId, parsedVisitorId, limit), context.RequestAborted);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetVisitCountsAsync(
        HttpContext context,
        Guid siteId,
        GetVisitCountWindowsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(tenantId.Value, siteId, context.RequestAborted);
        return Results.Ok(new VisitCountsResponse(result.Last7, result.Last30, result.Last90));
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
