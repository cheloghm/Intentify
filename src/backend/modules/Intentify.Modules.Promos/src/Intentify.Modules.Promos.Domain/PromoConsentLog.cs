namespace Intentify.Modules.Promos.Domain;

public sealed class PromoConsentLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid PromoEntryId { get; init; }
    public bool ConsentGiven { get; init; }
    public string ConsentStatement { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
