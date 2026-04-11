using System.Text.RegularExpressions;
using Intentify.Modules.LinkHub.Domain;

namespace Intentify.Modules.LinkHub.Application;

internal static class ProfileMapper
{
    internal static LinkHubProfileResult ToResult(LinkHubProfile profile) =>
        new(profile.Id, profile.TenantId, profile.Slug, profile.DisplayName, profile.Bio,
            profile.AvatarEmoji, profile.AvatarInitials, profile.BrandColor, profile.Theme,
            profile.IsPublished, profile.EngageBotEnabled, profile.WidgetKey, profile.SiteKey,
            profile.Links
                .OrderBy(l => l.Order)
                .Select(l => new LinkResult(l.Id, l.Label, l.Url, l.Platform, l.IconEmoji, l.Order, l.IsActive, l.ClickCount))
                .ToList());
}

public sealed class GetOrCreateProfileHandler(ILinkHubRepository repository)
{
    public async Task<LinkHubProfileResult> HandleAsync(GetOrCreateProfileQuery query, CancellationToken ct = default)
    {
        var profile = await repository.GetByTenantAsync(query.TenantId, ct);
        if (profile is null)
        {
            profile = new LinkHubProfile
            {
                TenantId    = query.TenantId,
                Slug        = $"user-{query.TenantId.ToString("N")[..8]}",
                DisplayName = string.Empty,
            };
            await repository.UpsertAsync(profile, ct);
        }
        return ProfileMapper.ToResult(profile);
    }
}

public sealed class SaveProfileHandler(ILinkHubRepository repository)
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9\-]{1,48}[a-z0-9]$", RegexOptions.Compiled);

    public async Task<(LinkHubProfileResult? Result, string? Error)> HandleAsync(SaveProfileCommand command, CancellationToken ct = default)
    {
        var slug = (command.Slug ?? string.Empty).Trim().ToLowerInvariant();

        if (slug.Length < 3 || slug.Length > 50 || !SlugRegex.IsMatch(slug))
            return (null, "Slug must be 3–50 characters, lowercase alphanumeric and hyphens only, and cannot start or end with a hyphen.");

        if (await repository.SlugExistsAsync(slug, command.TenantId, ct))
            return (null, "This slug is already taken. Please choose a different one.");

        var existing = await repository.GetByTenantAsync(command.TenantId, ct);
        var profile  = existing ?? new LinkHubProfile { TenantId = command.TenantId };

        profile.Slug             = slug;
        profile.DisplayName      = command.DisplayName;
        profile.Bio              = command.Bio;
        profile.AvatarEmoji      = command.AvatarEmoji;
        profile.AvatarInitials   = command.AvatarInitials;
        profile.BrandColor       = command.BrandColor ?? "#6366f1";
        profile.Theme            = command.Theme ?? "light";
        profile.IsPublished      = command.IsPublished;
        profile.EngageBotEnabled = command.EngageBotEnabled;
        profile.WidgetKey        = command.WidgetKey;
        profile.SiteKey          = command.SiteKey;
        profile.UpdatedAtUtc     = DateTime.UtcNow;

        profile.Links = command.Links.Select(l => new LinkHubLink
        {
            Id         = string.IsNullOrWhiteSpace(l.Id) ? Guid.NewGuid().ToString("N") : l.Id,
            Label      = l.Label,
            Url        = l.Url,
            Platform   = l.Platform,
            IconEmoji  = l.IconEmoji,
            Order      = l.Order,
            IsActive   = l.IsActive,
            ClickCount = existing?.Links.FirstOrDefault(x => x.Id == l.Id)?.ClickCount ?? 0,
        }).ToList();

        await repository.UpsertAsync(profile, ct);
        return (ProfileMapper.ToResult(profile), null);
    }
}

public sealed class GetPublicProfileHandler(ILinkHubRepository repository)
{
    public async Task<LinkHubProfileResult?> HandleAsync(GetPublicProfileQuery query, CancellationToken ct = default)
    {
        var profile = await repository.GetBySlugAsync(query.Slug, ct);
        if (profile is null || !profile.IsPublished) return null;
        return ProfileMapper.ToResult(profile);
    }
}

public sealed class RecordClickHandler(ILinkHubRepository repository)
{
    public async Task HandleAsync(RecordClickCommand command, CancellationToken ct = default)
    {
        var click = new LinkHubClick
        {
            ProfileId        = command.ProfileId,
            TenantId         = command.TenantId,
            LinkId           = command.LinkId,
            ReferrerRaw      = command.ReferrerRaw,
            ReferrerPlatform = ParsePlatform(command.ReferrerRaw),
            Device           = ParseDevice(command.UserAgent),
        };

        await repository.RecordClickAsync(click, ct);

        if (!string.IsNullOrWhiteSpace(command.LinkId))
            await repository.IncrementLinkClickAsync(command.ProfileId, command.LinkId, ct);
    }

    private static string ParsePlatform(string? referrer)
    {
        if (string.IsNullOrWhiteSpace(referrer)) return "direct";
        var r = referrer.ToLowerInvariant();
        if (r.Contains("facebook") || r.Contains("fb.com")) return "facebook";
        if (r.Contains("instagram"))                         return "instagram";
        if (r.Contains("tiktok"))                            return "tiktok";
        if (r.Contains("twitter") || r.Contains("x.com") || r.Contains("t.co")) return "x";
        if (r.Contains("linkedin"))                          return "linkedin";
        if (r.Contains("youtube") || r.Contains("youtu.be")) return "youtube";
        if (r.Contains("snapchat"))                          return "snapchat";
        if (r.Contains("pinterest"))                         return "pinterest";
        if (r.Contains("google"))                            return "google";
        if (r.Contains("whatsapp"))                          return "whatsapp";
        return "other";
    }

    private static string ParseDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "desktop";
        if (Regex.IsMatch(userAgent, "iPad|Tablet", RegexOptions.IgnoreCase)) return "tablet";
        if (Regex.IsMatch(userAgent, "Mobi|Android", RegexOptions.IgnoreCase)) return "mobile";
        return "desktop";
    }
}

public sealed class GetAnalyticsHandler(ILinkHubRepository repository)
{
    public async Task<LinkHubAnalyticsResult> HandleAsync(LinkHubAnalyticsQuery query, CancellationToken ct = default)
    {
        var from    = DateTime.UtcNow.AddDays(-query.Days).Date;
        var to      = DateTime.UtcNow;
        var profile = await repository.GetByTenantAsync(query.TenantId, ct);
        var clicks  = await repository.GetClicksAsync(query.TenantId, from, to, ct);

        var totalViews  = clicks.Count(c => c.LinkId is null);
        var totalClicks = clicks.Count(c => c.LinkId is not null);
        var total       = clicks.Count;

        var dailyClicks = clicks
            .GroupBy(c => c.ClickedAtUtc.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyClickPoint(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(c => c.LinkId is null),
                g.Count(c => c.LinkId is not null)))
            .ToList();

        var referrerBreakdown = clicks
            .GroupBy(c => c.ReferrerPlatform)
            .OrderByDescending(g => g.Count())
            .Select(g => new PlatformBreakdown(g.Key, g.Count(), total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0))
            .ToList();

        var deviceBreakdown = clicks
            .GroupBy(c => c.Device ?? "desktop")
            .OrderByDescending(g => g.Count())
            .Select(g => new PlatformBreakdown(g.Key, g.Count(), total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0))
            .ToList();

        var linkLookup = profile?.Links.ToDictionary(l => l.Id, l => l) ?? [];
        var topLinks = clicks
            .Where(c => c.LinkId is not null)
            .GroupBy(c => c.LinkId!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g =>
            {
                var link = linkLookup.TryGetValue(g.Key, out var l) ? l : null;
                return new LinkClickCount(g.Key, link?.Label ?? g.Key, link?.Platform, g.Count());
            })
            .ToList();

        return new LinkHubAnalyticsResult(totalViews, totalClicks, dailyClicks, referrerBreakdown, deviceBreakdown, topLinks);
    }
}
