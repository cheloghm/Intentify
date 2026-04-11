using System.Security.Claims;
using Intentify.Modules.LinkHub.Application;
using Intentify.Modules.LinkHub.Domain;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.LinkHub.Api;

internal static class LinkHubEndpoints
{
    // ── Admin: GET /linkhub/profile ────────────────────────────────────────
    public static async Task<IResult> GetProfileAsync(
        HttpContext context,
        GetOrCreateProfileHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(new GetOrCreateProfileQuery(tenantId.Value), context.RequestAborted);
        return Results.Ok(result);
    }

    // ── Admin: PUT /linkhub/profile ────────────────────────────────────────
    public static async Task<IResult> SaveProfileAsync(
        HttpContext context,
        SaveProfileRequest request,
        SaveProfileHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var command = new SaveProfileCommand(
            tenantId.Value,
            request.Slug,
            request.DisplayName,
            request.Bio,
            request.AvatarEmoji,
            request.AvatarInitials,
            request.ProfilePictureUrl,
            request.BackgroundType,
            request.BackgroundValue,
            request.BrandColor,
            request.Theme,
            request.IsPublished,
            request.EngageBotEnabled,
            request.WidgetKey,
            request.SiteKey,
            request.Links?.Select(l => new SaveLinkDto(l.Id, l.Label, l.Url, l.Platform, l.IconEmoji, l.Order, l.IsActive, l.DisplayMode)).ToList()
                ?? []);

        var (result, error) = await handler.HandleAsync(command, context.RequestAborted);
        if (error is not null)
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
                new Dictionary<string, string[]> { ["slug"] = [error] }));

        return Results.Ok(result);
    }

    // ── Admin: GET /linkhub/analytics ─────────────────────────────────────
    public static async Task<IResult> GetAnalyticsAsync(
        HttpContext context,
        int days,
        GetAnalyticsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(
            new LinkHubAnalyticsQuery(tenantId.Value, days > 0 ? days : 30),
            context.RequestAborted);
        return Results.Ok(result);
    }

    // ── Admin: POST /linkhub/profile/avatar ───────────────────────────────
    public static async Task<IResult> UploadAvatarAsync(
        HttpContext context,
        ILinkHubRepository repository)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (!context.Request.HasFormContentType)
            return Results.BadRequest(new { error = "Expected multipart/form-data." });

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "File must be an image." });

        if (file.Length > 5 * 1024 * 1024)
            return Results.BadRequest(new { error = "Image must be under 5MB." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, context.RequestAborted);
        var base64  = Convert.ToBase64String(ms.ToArray());
        var dataUri = $"data:{file.ContentType};base64,{base64}";

        var profile = await repository.GetByTenantAsync(tenantId.Value, context.RequestAborted);
        if (profile is null) return Results.NotFound();

        profile.ProfilePictureUrl = dataUri;
        profile.UpdatedAtUtc      = DateTime.UtcNow;
        await repository.UpsertAsync(profile, context.RequestAborted);

        return Results.Ok(new { url = dataUri });
    }

    // ── Public: GET /hub/{slug} ────────────────────────────────────────────
    public static async Task<IResult> GetPublicPageAsync(
        string slug,
        HttpContext context,
        GetPublicProfileHandler handler)
    {
        if (string.IsNullOrWhiteSpace(slug)) return Results.NotFound();

        var profile = await handler.HandleAsync(new GetPublicProfileQuery(slug.Trim().ToLowerInvariant()), context.RequestAborted);
        if (profile is null) return Results.NotFound();

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        var html    = LinkHubPublicPage.BuildHtml(profile, baseUrl);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    // ── Public: POST /hub/{slug}/view ──────────────────────────────────────
    public static async Task<IResult> RecordViewAsync(
        string slug,
        PublicClickRequest request,
        HttpContext context,
        ILinkHubRepository repository,
        RecordClickHandler handler)
    {
        var profile = await repository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), context.RequestAborted);
        if (profile is null) return Results.NotFound();

        await handler.HandleAsync(new RecordClickCommand(
            profile.Id, profile.TenantId,
            null,
            request.Referrer,
            GetClientIp(context),
            request.UserAgent), context.RequestAborted);

        return Results.Ok();
    }

    // ── Public: POST /hub/{slug}/click ─────────────────────────────────────
    public static async Task<IResult> RecordClickAsync(
        string slug,
        PublicClickRequest request,
        HttpContext context,
        ILinkHubRepository repository,
        RecordClickHandler handler)
    {
        var profile = await repository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), context.RequestAborted);
        if (profile is null) return Results.NotFound();

        await handler.HandleAsync(new RecordClickCommand(
            profile.Id, profile.TenantId,
            request.LinkId,
            request.Referrer,
            GetClientIp(context),
            request.UserAgent), context.RequestAborted);

        return Results.Ok();
    }

    private static string? GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("tenantId");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

// ── Request models ────────────────────────────────────────────────────────────

public sealed record SaveProfileRequest(
    string Slug,
    string DisplayName,
    string? Bio,
    string? AvatarEmoji,
    string? AvatarInitials,
    string? ProfilePictureUrl,
    string? BackgroundType,
    string? BackgroundValue,
    string? BrandColor,
    string? Theme,
    bool IsPublished,
    bool EngageBotEnabled,
    string? WidgetKey,
    string? SiteKey,
    IReadOnlyList<SaveLinkRequest>? Links);

public sealed record SaveLinkRequest(
    string Id,
    string Label,
    string Url,
    string? Platform,
    string? IconEmoji,
    int Order,
    bool IsActive,
    string DisplayMode = "icon-label");

public sealed record PublicClickRequest(
    string? LinkId,
    string? Referrer,
    string? UserAgent);
