namespace Intentify.Modules.LinkHub.Application;

public sealed record GetOrCreateProfileQuery(Guid TenantId);
public sealed record GetPublicProfileQuery(string Slug);
public sealed record SaveProfileCommand(
    Guid TenantId,
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
    IReadOnlyList<SaveLinkDto> Links);

public sealed record SaveLinkDto(
    string Id,
    string Label,
    string Url,
    string? Platform,
    string? IconEmoji,
    int Order,
    bool IsActive,
    string DisplayMode = "icon-label");

public sealed record RecordClickCommand(
    Guid ProfileId,
    Guid TenantId,
    string? LinkId,
    string? ReferrerRaw,
    string? IpAddress,
    string? UserAgent);

public sealed record LinkHubAnalyticsQuery(Guid TenantId, int Days = 30);

public sealed record LinkHubProfileResult(
    Guid Id,
    Guid TenantId,
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
    IReadOnlyList<LinkResult> Links);

public sealed record LinkResult(
    string Id,
    string Label,
    string Url,
    string? Platform,
    string? IconEmoji,
    int Order,
    bool IsActive,
    int ClickCount,
    string DisplayMode = "icon-label");

public sealed record LinkHubAnalyticsResult(
    int TotalViews,
    int TotalClicks,
    IReadOnlyList<DailyClickPoint> DailyClicks,
    IReadOnlyList<PlatformBreakdown> ReferrerBreakdown,
    IReadOnlyList<PlatformBreakdown> DeviceBreakdown,
    IReadOnlyList<LinkClickCount> TopLinks);

public sealed record DailyClickPoint(string Date, int Views, int Clicks);
public sealed record PlatformBreakdown(string Platform, int Count, double Pct);
public sealed record LinkClickCount(string LinkId, string Label, string? Platform, int Count);
