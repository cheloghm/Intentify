namespace Intentify.Modules.LinkHub.Domain;

public sealed class LinkHubLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? IconEmoji { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public int ClickCount { get; set; }
    public string DisplayMode { get; set; } = "icon-label";
}
