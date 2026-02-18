namespace Intentify.Modules.Promos.Domain;

public sealed class Promo
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PublicKey { get; init; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
}
