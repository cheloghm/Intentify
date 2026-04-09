using System.Security.Claims;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Intelligence.Api;

internal static class IntelligenceEndpoints
{
    // ── POST /intelligence/refresh ─────────────────────────────────────────

    public static async Task<IResult> RefreshAsync(
        RefreshIntelligenceApiRequest request,
        HttpContext context,
        RefreshIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (!Guid.TryParse(request.SiteId, out var siteId))
            return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(
            tenantId,
            new RefreshIntelligenceRequest(
                SiteId:          siteId,
                Category:        request.Category,
                Location:        request.Location,
                TimeWindow:      request.TimeWindow,
                Limit:           request.Limit,
                Keyword:         request.Keyword,
                AgeRange:        request.AgeRange,
                CategoryId:      request.CategoryId,
                SearchType:      request.SearchType,
                ComparisonTerms: ParseCommaSeparated(request.ComparisonTerms),
                SubRegion:       request.SubRegion),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(new
            {
                provider            = result.Value!.Provider,
                refreshedAtUtc      = result.Value.RefreshedAtUtc,
                itemsCount          = result.Value.ItemsCount,
                relatedQueriesCount = result.Value.RelatedQueriesCount,
                risingQueriesCount  = result.Value.RisingQueriesCount,
            }),
            OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/trends ───────────────────────────────────────────

    public static async Task<IResult> GetTrendsAsync(
        string siteId, string category, string location, string timeWindow,
        HttpContext context, QueryIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(tenantId, siteGuid, category, location, timeWindow, context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound         => Results.NotFound(),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/status ───────────────────────────────────────────

    public static async Task<IResult> GetStatusAsync(
        string siteId, string category, string location, string timeWindow,
        HttpContext context, GetIntelligenceStatusService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(tenantId, siteGuid, category, location, timeWindow, context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound         => Results.NotFound(),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/dashboard ────────────────────────────────────────

    public static async Task<IResult> GetDashboardAsync(
        string siteId,
        string? category,
        string? location,
        string? timeWindow,
        string? provider,
        string? keyword,
        string? audienceType,
        string? ageRange,
        int? categoryId,
        string? searchType,
        string? comparisonTerms,
        string? subRegion,
        int? limit,
        HttpContext context,
        QueryIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleDashboardAsync(
            tenantId,
            new IntelligenceDashboardQuery(
                siteGuid, category, location, timeWindow, provider, keyword,
                audienceType, limit, ageRange, categoryId, searchType, comparisonTerms, subRegion),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/site-summary ────────────────────────────────────

    public static async Task<IResult> GetSiteSummaryAsync(
        string siteId,
        string? category,
        string? location,
        string? timeWindow,
        string? provider,
        string? keyword,
        string? audienceType,
        string? ageRange,
        int? categoryId,
        string? searchType,
        string? comparisonTerms,
        string? subRegion,
        int? limit,
        HttpContext context,
        GetSiteInsightsSummaryService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(
            tenantId,
            new IntelligenceDashboardQuery(
                siteGuid, category, location, timeWindow, provider, keyword,
                audienceType, limit, ageRange, categoryId, searchType, comparisonTerms, subRegion),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound         => Results.NotFound(),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── PUT /intelligence/profiles/{siteId} ───────────────────────────────

    public static async Task<IResult> UpsertProfileAsync(
        string siteId, UpsertIntelligenceProfileRequest request,
        HttpContext context, UpsertIntelligenceProfileService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(
            tenantId,
            new Application.UpsertIntelligenceProfileRequest(
                siteGuid,
                request.ProfileName,
                request.IndustryCategory,
                request.PrimaryAudienceType,
                request.TargetLocations,
                request.PrimaryProductsOrServices,
                request.WatchTopics,
                request.SeasonalPriorities,
                request.IsActive,
                request.RefreshIntervalMinutes),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/profiles/{siteId} ───────────────────────────────

    public static async Task<IResult> GetProfileAsync(
        string siteId, HttpContext context, GetIntelligenceProfileService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(siteId, out var siteGuid)) return BadRequest("siteId", "Site id is invalid.");

        var result = await service.HandleAsync(tenantId, siteGuid, context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.Success          => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound         => Results.NotFound(),
            _                                => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ── GET /intelligence/network-signals ─────────────────────────────────

    public static async Task<IResult> GetNetworkSignalsAsync(
        INetworkSignalsService service,
        string? country = null,
        string? category = null,
        string? industry = null,
        int daysBack = 7)
    {
        var days = Math.Clamp(daysBack, 1, 90);
        var result = await service.GetNetworkSignalsAsync(
            new NetworkSignalsQuery(
                Country: string.IsNullOrWhiteSpace(country) ? null : country,
                ProductCategory: string.IsNullOrWhiteSpace(category) ? null : category,
                Industry: string.IsNullOrWhiteSpace(industry) ? null : industry,
                DaysBack: days));

        return Results.Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string? TryGetTenantId(ClaimsPrincipal user)
    {
        var t = user.FindFirstValue("tenantId");
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static IResult BadRequest(string field, string message) =>
        Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
            new Dictionary<string, string[]> { [field] = [message] }));

    private static IReadOnlyList<string>? ParseCommaSeparated(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
}

// ── API request model ─────────────────────────────────────────────────────────

internal sealed record RefreshIntelligenceApiRequest(
    string SiteId,
    string Category,
    string Location,
    string TimeWindow,
    int? Limit,
    string? Keyword         = null,
    string? AgeRange        = null,
    int? CategoryId         = null,
    string? SearchType      = null,
    string? ComparisonTerms = null,
    string? SubRegion       = null);
    