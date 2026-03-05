using System.Security.Claims;
using Intentify.Modules.Ads.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Ads.Api;

internal static class AdsEndpoints
{
    public static async Task<IResult> CreateCampaignAsync(HttpContext context, CreateAdCampaignRequest request, CreateAdCampaignHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(
            new CreateAdCampaignCommand(tenantId.Value, request.SiteId, request.Name, request.Objective, request.IsActive, request.StartsAtUtc, request.EndsAtUtc, request.Budget, MapPlacements(request.Placements)),
            context.RequestAborted);

        return ToResult(result);
    }

    public static async Task<IResult> ListCampaignsAsync(HttpContext context, string? siteId, ListAdCampaignsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        Guid? parsedSiteId = null;
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            if (!Guid.TryParse(siteId, out var parsed))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] }));
            }

            parsedSiteId = parsed;
        }

        var result = await handler.HandleAsync(new ListAdCampaignsQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> GetCampaignAsync(HttpContext context, string campaignId, GetAdCampaignHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        var result = await handler.HandleAsync(new GetAdCampaignQuery(tenantId!.Value, parsedCampaignId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> UpdateCampaignAsync(HttpContext context, string campaignId, UpdateAdCampaignRequest request, UpdateAdCampaignHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        var result = await handler.HandleAsync(
            new UpdateAdCampaignCommand(tenantId!.Value, parsedCampaignId, request.SiteId, request.Name, request.Objective, request.IsActive, request.StartsAtUtc, request.EndsAtUtc, request.Budget),
            context.RequestAborted);

        return ToResult(result);
    }

    public static async Task<IResult> UpsertPlacementsAsync(HttpContext context, string campaignId, UpsertAdPlacementsRequest request, UpsertAdPlacementsHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        var result = await handler.HandleAsync(new UpsertAdPlacementsCommand(tenantId!.Value, parsedCampaignId, MapPlacements(request.Placements)), context.RequestAborted);
        return ToResult(result);
    }

    public static async Task<IResult> ActivateAsync(HttpContext context, string campaignId, SetAdCampaignActiveHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        var result = await handler.HandleAsync(new SetAdCampaignActiveCommand(tenantId!.Value, parsedCampaignId, true), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> DeactivateAsync(HttpContext context, string campaignId, SetAdCampaignActiveHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        var result = await handler.HandleAsync(new SetAdCampaignActiveCommand(tenantId!.Value, parsedCampaignId, false), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> GetReportAsync(HttpContext context, string campaignId, string? fromUtc, string? toUtc, GetAdCampaignReportHandler handler)
    {
        var parsed = ParseCampaignAndTenantId(context, campaignId, out var tenantId, out var parsedCampaignId);
        if (parsed is not null) return parsed;

        DateTime? parsedFrom = null;
        DateTime? parsedTo = null;
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(fromUtc) && !DateTime.TryParse(fromUtc, out var fromDate)) errors["fromUtc"] = ["fromUtc is invalid."];
        if (!string.IsNullOrWhiteSpace(fromUtc) && DateTime.TryParse(fromUtc, out var fromValue)) parsedFrom = fromValue;
        if (!string.IsNullOrWhiteSpace(toUtc) && !DateTime.TryParse(toUtc, out var toDate)) errors["toUtc"] = ["toUtc is invalid."];
        if (!string.IsNullOrWhiteSpace(toUtc) && DateTime.TryParse(toUtc, out var toValue)) parsedTo = toValue;

        if (errors.Count > 0) return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));

        var result = await handler.HandleAsync(new GetAdCampaignReportQuery(tenantId!.Value, parsedCampaignId, parsedFrom, parsedTo), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    private static IResult? ParseCampaignAndTenantId(HttpContext context, string campaignId, out Guid? tenantId, out Guid parsedCampaignId)
    {
        tenantId = TryGetTenantId(context.User);
        parsedCampaignId = Guid.Empty;
        if (tenantId is null) return Results.Unauthorized();

        if (!Guid.TryParse(campaignId, out parsedCampaignId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["campaignId"] = ["Campaign id is invalid."]
            }));
        }

        return null;
    }

    private static IReadOnlyCollection<AdPlacementInput>? MapPlacements(IReadOnlyCollection<AdPlacementRequest>? placements)
    {
        return placements?.Select(item => new AdPlacementInput(item.SlotKey, item.PathPattern, item.Device, item.Headline, item.Body, item.ImageUrl, item.DestinationUrl, item.CtaText, item.Order, item.IsActive)).ToArray();
    }

    private static IResult ToResult<T>(OperationResult<T> result)
    {
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(result.Value)
        };
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
