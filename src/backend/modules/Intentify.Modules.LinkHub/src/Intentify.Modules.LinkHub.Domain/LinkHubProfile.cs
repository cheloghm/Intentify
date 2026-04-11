namespace Intentify.Modules.LinkHub.Domain;

public sealed class LinkHubProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarEmoji { get; set; } = "👤";
    public string? AvatarInitials { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? BackgroundType { get; set; } = "color";
    public string? BackgroundValue { get; set; } = "#ffffff";
    public string? BrandColor { get; set; } = "#6366f1";
    public string? Theme { get; set; } = "light";
    public bool IsPublished { get; set; } = false;
    public bool EngageBotEnabled { get; set; } = false;
    public string? WidgetKey { get; set; }
    public string? SiteKey { get; set; }
    public List<LinkHubLink> Links { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
