namespace Intentify.Modules.Promos.Domain;

public sealed class PromoEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid PromoId { get; init; }
    public Guid? VisitorId { get; init; }
    public string? FirstPartyId { get; init; }
    public string? SessionId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public IReadOnlyDictionary<string, string>? Answers { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
