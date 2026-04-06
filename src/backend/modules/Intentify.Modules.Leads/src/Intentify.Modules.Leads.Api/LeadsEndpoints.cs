using System.Security.Claims;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Leads.Domain;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Leads.Api;

internal static class LeadsEndpoints
{
    public static async Task<IResult> ListAsync(
        HttpContext context, string? siteId, int page, int pageSize, ListLeadsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        Guid? parsedSiteId = null;
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            if (!Guid.TryParse(siteId, out var parsed)) return BadRequest("siteId", "Site id is invalid.");
            parsedSiteId = parsed;
        }

        page     = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var leads = await handler.HandleAsync(new ListLeadsQuery(tenantId.Value, parsedSiteId, page, pageSize), context.RequestAborted);
        return Results.Ok(leads);
    }

    public static async Task<IResult> GetAsync(
        HttpContext context, string leadId, GetLeadHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(leadId, out var parsedLeadId)) return BadRequest("leadId", "Lead id is invalid.");

        var result = await handler.HandleAsync(new GetLeadQuery(tenantId.Value, parsedLeadId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> GetByVisitorAsync(
        HttpContext context, string siteId, string visitorId, GetLeadByVisitorIdHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid))     return BadRequest("siteId",    "Site id is invalid.");
        if (!Guid.TryParse(visitorId, out var visitorGuid)) return BadRequest("visitorId", "Visitor id is invalid.");

        var lead = await handler.HandleAsync(new GetLeadByVisitorIdQuery(tenantId.Value, siteGuid, visitorGuid), context.RequestAborted);
        return lead is null ? Results.NotFound() : Results.Ok(lead);
    }

    // ── PATCH /leads/{leadId}/stage ────────────────────────────────────────
    // Phase 4: Move a lead to a different pipeline stage.
    // Updates opportunityLabel which drives the Kanban board position.

    public static async Task<IResult> PatchStageAsync(
        HttpContext context,
        string leadId,
        PatchLeadStageRequest request,
        GetLeadHandler getHandler,
        ILeadRepository repo)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(leadId, out var parsedId)) return BadRequest("leadId", "Lead id is invalid.");

        var result = await getHandler.HandleAsync(new GetLeadQuery(tenantId.Value, parsedId), context.RequestAborted);
        if (result.Status == OperationStatus.NotFound || result.Value is null) return Results.NotFound();

        var lead = result.Value;
        lead.OpportunityLabel = request.Stage?.Trim();
        lead.UpdatedAtUtc = DateTime.UtcNow;
        await repo.ReplaceAsync(lead, context.RequestAborted);
        return Results.Ok(lead);
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var v = user.FindFirstValue("tenantId");
        return Guid.TryParse(v, out var id) ? id : null;
    }

    private static IResult BadRequest(string field, string message) =>
        Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = [message] }));
}

internal sealed record PatchLeadStageRequest(string? Stage);
