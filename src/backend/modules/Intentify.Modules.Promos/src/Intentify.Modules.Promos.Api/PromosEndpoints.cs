using System.Security.Claims;
using Intentify.Modules.Promos.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Promos.Api;

internal static class PromosEndpoints
{
    public static async Task<IResult> CreatePromoAsync(HttpContext context, CreatePromoRequest request, CreatePromoHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(new CreatePromoCommand(tenantId.Value, request.SiteId, request.Name, request.Description, request.IsActive), context.RequestAborted);
        return result.Status == OperationStatus.ValidationFailed
            ? Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors))
            : Results.Ok(result.Value);
    }

    public static async Task<IResult> ListPromosAsync(HttpContext context, string? siteId, ListPromosHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        Guid? parsedSiteId = null;
        if (!string.IsNullOrWhiteSpace(siteId) && !Guid.TryParse(siteId, out var parsed))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] }));
        }
        else if (!string.IsNullOrWhiteSpace(siteId))
        {
            parsedSiteId = Guid.Parse(siteId!);
        }

        var promos = await handler.HandleAsync(new ListPromosQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return Results.Ok(promos);
    }

    public static async Task<IResult> ListEntriesAsync(HttpContext context, string promoId, int page, int pageSize, ListPromoEntriesHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(promoId, out var parsedPromoId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["promoId"] = ["Promo id is invalid."] }));
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var result = await handler.HandleAsync(new ListPromoEntriesQuery(tenantId.Value, parsedPromoId, page, pageSize), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> CreatePublicEntryAsync(HttpContext context, string promoKey, CreatePublicPromoEntryRequest request, CreatePublicPromoEntryHandler handler)
    {
        var result = await handler.HandleAsync(
            new CreatePublicPromoEntryCommand(
                promoKey,
                request.VisitorId,
                request.FirstPartyId,
                request.SessionId,
                request.Email,
                request.Name,
                request.ConsentGiven,
                request.ConsentStatement),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value)
        };
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
