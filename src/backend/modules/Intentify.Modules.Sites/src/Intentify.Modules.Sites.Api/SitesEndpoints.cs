using System.Security.Claims;
using System.Security.Cryptography;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Intentify.Modules.Sites.Api;

internal static class SitesEndpoints
{
    private const int KeyBytes = 32;
    private const int MaxDomainLength = 255;

    public static async Task<IResult> CreateSiteAsync(
        CreateSiteRequest request,
        HttpContext context,
        IMongoDatabase database)
    {
        var errors = ValidateCreateSiteRequest(request);
        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var normalizedDomain = NormalizeDomain(request.Domain);
        if (normalizedDomain is null)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["domain"] = ["Domain is invalid."]
            }));
        }

        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var duplicate = await sites.Find(site => site.TenantId == tenantId.Value && site.Domain == normalizedDomain)
            .FirstOrDefaultAsync();
        if (duplicate is not null)
        {
            return Results.Conflict();
        }

        var now = DateTime.UtcNow;
        var site = new Site
        {
            TenantId = tenantId.Value,
            Domain = normalizedDomain,
            AllowedOrigins = [],
            SiteKey = GenerateKey(),
            WidgetKey = GenerateKey(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await sites.InsertOneAsync(site);

        return Results.Ok(new CreateSiteResponse(
            site.Id.ToString("N"),
            site.Domain,
            site.AllowedOrigins,
            site.SiteKey,
            site.WidgetKey));
    }

    public static async Task<IResult> ListSitesAsync(HttpContext context, IMongoDatabase database)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var results = await sites.Find(site => site.TenantId == tenantId.Value).ToListAsync();

        var response = results
            .Select(site => ToSummaryResponse(site))
            .ToArray();

        return Results.Ok(response);
    }

    public static async Task<IResult> UpdateAllowedOriginsAsync(
        string siteId,
        UpdateAllowedOriginsRequest request,
        HttpContext context,
        IMongoDatabase database)
    {
        var errors = ValidateAllowedOriginsRequest(request);
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

        var normalizedOrigins = NormalizeOrigins(request.AllowedOrigins);
        if (normalizedOrigins is null)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["allowedOrigins"] = ["Allowed origins must be valid absolute HTTP/HTTPS origins without paths."]
            }));
        }

        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteGuid) &
            Builders<Site>.Filter.Eq(site => site.TenantId, tenantId.Value);
        var update = Builders<Site>.Update
            .Set(site => site.AllowedOrigins, normalizedOrigins)
            .Set(site => site.UpdatedAtUtc, DateTime.UtcNow);

        var updated = await sites.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After });

        if (updated is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToSummaryResponse(updated));
    }

    public static async Task<IResult> RegenerateKeysAsync(
        string siteId,
        HttpContext context,
        IMongoDatabase database)
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

        var newSiteKey = GenerateKey();
        var newWidgetKey = GenerateKey();
        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var filter = Builders<Site>.Filter.Eq(site => site.Id, siteGuid) &
            Builders<Site>.Filter.Eq(site => site.TenantId, tenantId.Value);
        var update = Builders<Site>.Update
            .Set(site => site.SiteKey, newSiteKey)
            .Set(site => site.WidgetKey, newWidgetKey)
            .Set(site => site.UpdatedAtUtc, DateTime.UtcNow);

        var updated = await sites.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Site> { ReturnDocument = ReturnDocument.After });

        if (updated is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new RegenerateKeysResponse(updated.SiteKey, updated.WidgetKey));
    }

    public static async Task<IResult> GetInstallationStatusAsync(
        string siteId,
        HttpContext context,
        IMongoDatabase database)
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

        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var site = await sites.Find(candidate => candidate.Id == siteGuid && candidate.TenantId == tenantId.Value)
            .FirstOrDefaultAsync();
        if (site is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToInstallationStatusResponse(site));
    }

    public static async Task<IResult> GetPublicInstallationStatusAsync(
        HttpContext context,
        IMongoDatabase database)
    {
        var widgetKey = context.Request.Query["widgetKey"].ToString();
        if (string.IsNullOrWhiteSpace(widgetKey))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["widgetKey"] = ["Widget key is required."]
            }));
        }

        var origin = TryResolveOrigin(context.Request);
        if (origin is null)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["origin"] = ["Origin or Referer header is required to determine the request origin."]
            }));
        }

        var sites = database.GetCollection<Site>(SitesMongoCollections.Sites);
        var site = await sites.Find(candidate => candidate.WidgetKey == widgetKey).FirstOrDefaultAsync();
        if (site is null)
        {
            return Results.NotFound();
        }

        if (!site.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
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

        return Results.Ok(ToInstallationStatusResponse(site));
    }

    private static Dictionary<string, string[]> ValidateCreateSiteRequest(CreateSiteRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null || string.IsNullOrWhiteSpace(request.Domain))
        {
            errors["domain"] = ["Domain is required."];
            return errors;
        }

        var normalized = NormalizeDomain(request.Domain);
        if (normalized is null)
        {
            errors["domain"] = ["Domain must be a valid hostname (localhost allowed)."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateAllowedOriginsRequest(UpdateAllowedOriginsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null || request.AllowedOrigins is null)
        {
            errors["allowedOrigins"] = ["Allowed origins are required."];
            return errors;
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in request.AllowedOrigins)
        {
            if (!TryNormalizeOrigin(origin, out var normalizedOrigin))
            {
                errors["allowedOrigins"] = ["Allowed origins must be valid absolute HTTP/HTTPS origins without paths."];
                return errors;
            }

            if (!normalized.Add(normalizedOrigin))
            {
                errors["allowedOrigins"] = ["Allowed origins must not contain duplicates."];
                return errors;
            }
        }

        return errors;
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

    private static string? NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var trimmed = domain.Trim().ToLowerInvariant();
        if (trimmed.Length > MaxDomainLength)
        {
            return null;
        }

        if (trimmed == "localhost")
        {
            return trimmed;
        }

        if (trimmed.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        if (!trimmed.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return trimmed;
    }

    private static List<string>? NormalizeOrigins(IReadOnlyCollection<string> origins)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in origins)
        {
            if (!TryNormalizeOrigin(origin, out var normalizedOrigin))
            {
                return null;
            }

            normalized.Add(normalizedOrigin);
        }

        return normalized.ToList();
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

        if (uri.Scheme is not (Uri.UriSchemeHttp or Uri.UriSchemeHttps))
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

    private static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyBytes);
        var raw = Convert.ToBase64String(bytes);
        return raw.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static SiteSummaryResponse ToSummaryResponse(Site site)
    {
        return new SiteSummaryResponse(
            site.Id.ToString("N"),
            site.Domain,
            site.AllowedOrigins,
            site.CreatedAtUtc,
            site.UpdatedAtUtc,
            ToInstallationStatusResponse(site));
    }

    private static InstallationStatusResponse ToInstallationStatusResponse(Site site)
    {
        var allowedCount = site.AllowedOrigins.Count;
        var isConfigured = allowedCount > 0;

        return new InstallationStatusResponse(
            site.Id.ToString("N"),
            site.Domain,
            isConfigured,
            allowedCount);
    }
}
