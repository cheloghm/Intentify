using System.Security.Claims;
using Intentify.Modules.Leads.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Leads.Api;

internal static class LeadsEndpoints
{
    public static async Task<IResult> ListAsync(HttpContext context, string? siteId, int page, int pageSize, ListLeadsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        Guid? parsedSiteId = null;
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            if (!Guid.TryParse(siteId, out var parsed))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["siteId"] = ["Site id is invalid."]
                }));
            }
            parsedSiteId = parsed;
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var leads = await handler.HandleAsync(new ListLeadsQuery(tenantId.Value, parsedSiteId, page, pageSize), context.RequestAborted);
        return Results.Ok(leads);
    }

    public static async Task<IResult> GetAsync(HttpContext context, string leadId, GetLeadHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (!Guid.TryParse(leadId, out var parsedLeadId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["leadId"] = ["Lead id is invalid."]
            }));
        }

        var result = await handler.HandleAsync(new GetLeadQuery(tenantId.Value, parsedLeadId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
