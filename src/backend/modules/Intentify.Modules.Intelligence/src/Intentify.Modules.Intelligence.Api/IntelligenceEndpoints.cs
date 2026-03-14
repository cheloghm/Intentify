using System.Security.Claims;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Intelligence.Api;

internal static class IntelligenceEndpoints
{
    public static async Task<IResult> RefreshAsync(
        RefreshIntelligenceRequest request,
        HttpContext context,
        RefreshIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(request.SiteId, out var siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleAsync(
            tenantId,
            new Application.RefreshIntelligenceRequest(siteId, request.Category, request.Location, request.TimeWindow, request.Limit),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(new
            {
                provider = result.Value!.Provider,
                refreshedAtUtc = result.Value.RefreshedAtUtc,
                itemsCount = result.Value.ItemsCount
            }),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public static async Task<IResult> GetTrendsAsync(
        string siteId,
        string category,
        string location,
        string timeWindow,
        HttpContext context,
        QueryIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleAsync(tenantId, siteGuid, category, location, timeWindow, context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public static async Task<IResult> GetStatusAsync(
        string siteId,
        string category,
        string location,
        string timeWindow,
        HttpContext context,
        GetIntelligenceStatusService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleAsync(tenantId, siteGuid, category, location, timeWindow, context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public static async Task<IResult> GetDashboardAsync(
        string siteId,
        string? category,
        string? location,
        string? timeWindow,
        string? provider,
        string? keyword,
        string? audienceType,
        int? limit,
        HttpContext context,
        QueryIntelligenceTrendsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleDashboardAsync(
            tenantId,
            new IntelligenceDashboardQuery(siteGuid, category, location, timeWindow, provider, keyword, audienceType, limit),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public static async Task<IResult> GetSiteSummaryAsync(
        string siteId,
        string? category,
        string? location,
        string? timeWindow,
        string? provider,
        string? keyword,
        string? audienceType,
        int? limit,
        HttpContext context,
        GetSiteInsightsSummaryService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleAsync(
            tenantId,
            new IntelligenceDashboardQuery(siteGuid, category, location, timeWindow, provider, keyword, audienceType, limit),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }


    public static async Task<IResult> UpsertProfileAsync(
        string siteId,
        UpsertIntelligenceProfileRequest request,
        HttpContext context,
        UpsertIntelligenceProfileService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

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
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    public static async Task<IResult> GetProfileAsync(
        string siteId,
        HttpContext context,
        GetIntelligenceProfileService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var result = await service.HandleAsync(tenantId, siteGuid, context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(result.Value),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static string? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantId = user.FindFirstValue("tenantId");
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}
