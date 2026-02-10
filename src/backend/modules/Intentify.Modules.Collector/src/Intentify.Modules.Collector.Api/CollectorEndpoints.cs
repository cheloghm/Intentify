using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Intentify.Modules.Collector.Api;

internal static class CollectorEndpoints
{
    private const int MaxContentLengthBytes = 32 * 1024;
    private const int MaxSiteKeyLength = 256;
    private const int MaxTypeLength = 64;
    private const int MaxUrlLength = 2048;
    private const int MaxReferrerLength = 2048;
    private const string SitesCollectionName = "sites.sites";
    private const string TrackerResourceName = "Intentify.Modules.Collector.Api.assets.tracker.js";

    public static async Task<IResult> GetTrackerAsync()
    {
        var assembly = typeof(CollectorModule).Assembly;
        await using var stream = assembly.GetManifestResourceStream(TrackerResourceName);
        if (stream is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return Results.Text(content, "application/javascript; charset=utf-8");
    }

    public static async Task<IResult> CollectEventAsync(
        CollectorEventRequest? request,
        HttpContext context,
        IMongoDatabase database)
    {
        if (context.Request.ContentLength is > MaxContentLengthBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var origin = TryResolveOrigin(context.Request);
        if (origin is null)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["origin"] = ["Origin or Referer header is required to determine the request origin."]
            }));
        }

        var sites = database.GetCollection<Site>(SitesCollectionName);
        var site = await sites.Find(candidate => candidate.SiteKey == request!.SiteKey.Trim())
            .FirstOrDefaultAsync();
        if (site is null)
        {
            return Results.NotFound();
        }

        if (!site.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var now = DateTime.UtcNow;
        var occurredAt = request!.TsUtc?.ToUniversalTime() ?? now;
        var normalizedType = request.Type.Trim();
        var normalizedUrl = request.Url.Trim();
        var normalizedReferrer = string.IsNullOrWhiteSpace(request.Referrer) ? null : request.Referrer.Trim();

        var events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        var collectorEvent = new CollectorEvent
        {
            SiteId = site.Id,
            TenantId = site.TenantId,
            Type = normalizedType,
            Url = normalizedUrl,
            Referrer = normalizedReferrer,
            OccurredAtUtc = occurredAt,
            ReceivedAtUtc = now,
            Origin = origin
        };

        await events.InsertOneAsync(collectorEvent);

        if (site.FirstEventReceivedAtUtc is null)
        {
            var filter = Builders<Site>.Filter.Eq(candidate => candidate.Id, site.Id) &
                Builders<Site>.Filter.Eq(candidate => candidate.FirstEventReceivedAtUtc, null);
            var update = Builders<Site>.Update
                .Set(candidate => candidate.FirstEventReceivedAtUtc, now)
                .Set(candidate => candidate.UpdatedAtUtc, now);
            await sites.UpdateOneAsync(filter, update);
        }

        return Results.Ok();
    }

    private static Dictionary<string, string[]> ValidateRequest(CollectorEventRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.SiteKey))
        {
            errors["siteKey"] = ["Site key is required."];
        }
        else if (request.SiteKey.Trim().Length > MaxSiteKeyLength)
        {
            errors["siteKey"] = ["Site key is too long."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Event type is required."];
        }
        else if (request.Type.Trim().Length > MaxTypeLength)
        {
            errors["type"] = ["Event type is too long."];
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            errors["url"] = ["Url is required."];
        }
        else if (request.Url.Trim().Length > MaxUrlLength)
        {
            errors["url"] = ["Url is too long."];
        }
        else if (!TryNormalizeAbsoluteUrl(request.Url, out _))
        {
            errors["url"] = ["Url must be an absolute HTTP/HTTPS URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.Referrer))
        {
            if (request.Referrer.Trim().Length > MaxReferrerLength)
            {
                errors["referrer"] = ["Referrer is too long."];
            }
            else if (!TryNormalizeAbsoluteUrl(request.Referrer, out _))
            {
                errors["referrer"] = ["Referrer must be an absolute HTTP/HTTPS URL."];
            }
        }

        return errors;
    }

    private static bool TryNormalizeAbsoluteUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.ToString();
        return true;
    }

    private static bool TryNormalizeOrigin(string? origin, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var trimmed = origin.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        if (uri.PathAndQuery is not ("" or "/"))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static string? TryResolveOrigin(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out var originValues))
        {
            var headerOrigin = originValues.ToString();
            if (TryNormalizeOrigin(headerOrigin, out var normalized))
            {
                return normalized;
            }
        }

        if (request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var referer = refererValues.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                var candidate = uri.GetLeftPart(UriPartial.Authority);
                if (TryNormalizeOrigin(candidate, out var normalized))
                {
                    return normalized;
                }
            }
        }

        return null;
    }
}
