namespace Intentify.Modules.LinkHub.Domain;

public sealed class LinkHubClick
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Guid TenantId { get; set; }
    public string? LinkId { get; set; }
    public string? ReferrerRaw { get; set; }
    public string ReferrerPlatform { get; set; } = "direct";
    public string? Country { get; set; }
    public string? Device { get; set; }
    public DateTime ClickedAtUtc { get; set; } = DateTime.UtcNow;
}
