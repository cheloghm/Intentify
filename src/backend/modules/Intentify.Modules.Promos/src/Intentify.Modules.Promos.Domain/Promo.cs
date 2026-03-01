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
    public string? FlyerFileName { get; set; }
    public string? FlyerContentType { get; set; }
    public byte[]? FlyerBytes { get; set; }
    public long? FlyerSizeBytes { get; set; }
    public IReadOnlyCollection<PromoQuestion> Questions { get; set; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed record PromoQuestion(
    string Key,
    string Label,
    string Type,
    bool Required,
    int Order);
