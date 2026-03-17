using System.Security.Claims;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Sites.Api;

internal static class SitesEndpoints
{
    public static async Task<IResult> CreateSiteAsync(
        CreateSiteRequest request,
        HttpContext context,
        CreateSiteHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }
        var result = await handler.HandleAsync(new CreateSiteCommand(tenantId.Value, request.Domain, request.Description, request.Category, request.Tags));

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.Conflict => Results.Conflict(),
            _ => Results.Ok(new CreateSiteResponse(
                result.Value!.Id.ToString("N"),
                result.Value.Domain,
                result.Value.Description,
                result.Value.Category,
                result.Value.Tags,
                result.Value.AllowedOrigins,
                result.Value.SiteKey,
                result.Value.WidgetKey))
        };
    }

    public static async Task<IResult> UpdateSiteProfileAsync(
        string siteId,
        UpdateSiteProfileRequest request,
        HttpContext context,
        UpdateSiteProfileHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new UpdateSiteProfileCommand(
            tenantId.Value,
            siteGuid,
            request.Description,
            request.Category,
            request.Tags));

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(ToSummaryResponse(result.Value!))
        };
    }

    public static async Task<IResult> ListSitesAsync(HttpContext context, ListSitesHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(tenantId.Value);

        var response = result.Value!
            .Select(site => ToSummaryResponse(site))
            .ToArray();

        return Results.Ok(response);
    }

    public static async Task<IResult> UpdateAllowedOriginsAsync(
        string siteId,
        UpdateAllowedOriginsRequest request,
        HttpContext context,
        UpdateAllowedOriginsHandler handler)
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

        var result = await handler.HandleAsync(new UpdateAllowedOriginsCommand(
            tenantId.Value,
            siteGuid,
            request.AllowedOrigins ?? Array.Empty<string>()));

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(ToSummaryResponse(result.Value!))
        };
    }

    public static async Task<IResult> RegenerateKeysAsync(
        string siteId,
        HttpContext context,
        RotateKeysHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new RotateKeysCommand(tenantId.Value, siteGuid));

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new RegenerateKeysResponse(result.Value!.SiteKey, result.Value.WidgetKey))
        };
    }

    public static async Task<IResult> GetSiteKeysAsync(
        string siteId,
        HttpContext context,
        GetSiteKeysHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new GetSiteKeysCommand(tenantId.Value, siteGuid));

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new SiteKeysResponse(result.Value!.SiteKey, result.Value.WidgetKey))
        };
    }

    public static async Task<IResult> GetInstallationStatusAsync(
        string siteId,
        HttpContext context,
        GetInstallationStatusHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new GetInstallationStatusCommand(tenantId.Value, siteGuid));

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(ToInstallationStatusResponse(result.Value!))
        };
    }

    public static async Task<IResult> GetInstallationDiagnosticsAsync(
        string siteId,
        string? siteKey,
        HttpContext context,
        GetInstallationDiagnosticsHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new GetInstallationDiagnosticsCommand(
            tenantId.Value,
            siteGuid,
            siteKey,
            null,
            TryResolveOrigin(context.Request)), context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(ToInstallationDiagnosticsResponse(result.Value!))
        };
    }

    public static async Task<IResult> GetPublicInstallationStatusAsync(
        HttpContext context,
        GetPublicInstallationStatusHandler handler)
    {
        var widgetKey = context.Request.Query["widgetKey"].ToString();
        var origin = TryResolveOrigin(context.Request);

        var result = await handler.HandleAsync(new GetPublicInstallationStatusCommand(widgetKey, origin));

        if (result.Status == OperationStatus.ValidationFailed)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors));
        }

        if (result.Status == OperationStatus.NotFound)
        {
            return Results.NotFound();
        }

        if (result.Status == OperationStatus.Forbidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        context.Response.Headers["Access-Control-Allow-Origin"] = result.Value!.Origin;
        var varyHeader = context.Response.Headers["Vary"].ToString();
        if (string.IsNullOrWhiteSpace(varyHeader))
        {
            context.Response.Headers["Vary"] = "Origin";
        }
        else if (!varyHeader
            .Split(',', StringSplitOptions.TrimEntries)
            .Any(value => value.Equals("Origin", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Headers["Vary"] = $"{varyHeader}, Origin";
        }

        return Results.Ok(ToInstallationStatusResponse(result.Value.Site));
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return null;
        }

        if (Guid.TryParse(tenantIdValue, out var tenantGuid))
        {
            return tenantGuid;
        }

        return null;
    }

    private static string? TryResolveOrigin(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out var originValues))
        {
            return originValues.ToString();
        }

        if (request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var referer = refererValues.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Authority);
            }
        }

        return null;
    }

    private static SiteSummaryResponse ToSummaryResponse(Site site)
    {
        return new SiteSummaryResponse(
            site.Id.ToString("N"),
            site.Domain,
            site.Description,
            site.Category,
            site.Tags,
            site.AllowedOrigins,
            site.CreatedAtUtc,
            site.UpdatedAtUtc,
            ToInstallationStatusResponse(site));
    }

    private static InstallationStatusResponse ToInstallationStatusResponse(Site site)
    {
        var allowedCount = site.AllowedOrigins.Count;
        var isConfigured = allowedCount > 0;
        var firstEventReceivedAtUtc = site.FirstEventReceivedAtUtc;
        var isInstalled = firstEventReceivedAtUtc is not null;

        return new InstallationStatusResponse(
            site.Id.ToString("N"),
            site.Domain,
            isConfigured,
            allowedCount,
            isInstalled,
            firstEventReceivedAtUtc);
    }

    private static InstallationDiagnosticsResponse ToInstallationDiagnosticsResponse(InstallationDiagnosticsResult result)
    {
        return new InstallationDiagnosticsResponse(
            result.Site.Id.ToString("N"),
            result.Site.Domain,
            result.SiteKeyValid,
            result.NormalizedOrigin,
            result.OriginAllowed,
            result.SdkScriptExpected,
            result.FirstEventSeen,
            result.Site.FirstEventReceivedAtUtc);
    }
}
