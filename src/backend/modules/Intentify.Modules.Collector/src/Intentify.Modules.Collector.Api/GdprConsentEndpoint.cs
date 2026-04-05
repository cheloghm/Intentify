using Intentify.Modules.Collector.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Collector.Api;

internal static class GdprConsentEndpoint
{
    private const int MaxSiteKeyLength = 256;
    private const int MaxVisitorIdLength = 128;

    public static async Task<IResult> HandleAsync(
        GdprConsentRequest? request,
        HttpContext context,
        ISiteLookupRepository siteLookupRepository,
        IVisitorConsentWriter consentWriter)
    {
        if (context.Request.ContentLength is > 4096)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var siteKey = request.SiteKey?.Trim();
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            errors["siteKey"] = ["Site key is required."];
        }
        else if (siteKey.Length > MaxSiteKeyLength)
        {
            errors["siteKey"] = ["Site key is too long."];
        }

        var visitorIdRaw = request.VisitorId?.Trim();
        if (string.IsNullOrWhiteSpace(visitorIdRaw))
        {
            errors["visitorId"] = ["Visitor id is required."];
        }
        else if (visitorIdRaw.Length > MaxVisitorIdLength)
        {
            errors["visitorId"] = ["Visitor id is too long."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var site = await siteLookupRepository.GetBySiteKeyAsync(siteKey!, context.RequestAborted);
        if (site is null)
        {
            return Results.NotFound();
        }

        if (!Guid.TryParse(visitorIdRaw, out var visitorId))
        {
            errors["visitorId"] = ["Visitor id must be a valid GUID."];
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var result = await consentWriter.RecordConsentAsync(new RecordVisitorConsentCommand(
            TenantId:     site.TenantId,
            SiteId:       site.SiteId,
            VisitorId:    visitorId,
            ConsentGiven: request.ConsentGiven,
            Version:      request.Version ?? "1.0"),
            context.RequestAborted);

        // Return 200 even when the visitor isn't found yet — the banner fires before
        // the first tracker event lands and creates the visitor record. The client
        // should re-send consent on the next page load once a visitor ID is confirmed.
        return Results.Ok(new { recorded = result.Recorded });
    }
}

public sealed record GdprConsentRequest(
    string SiteKey,
    string VisitorId,
    bool ConsentGiven,
    string? Version = null);
