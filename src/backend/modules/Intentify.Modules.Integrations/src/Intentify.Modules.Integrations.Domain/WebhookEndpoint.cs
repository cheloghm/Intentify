namespace Intentify.Modules.Integrations.Domain;

public sealed class WebhookEndpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>"generic" or "slack"</summary>
    public string Type { get; set; } = "generic";

    /// <summary>Comma-separated event names: "lead.created,visitor.identified"</summary>
    public string Events { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
