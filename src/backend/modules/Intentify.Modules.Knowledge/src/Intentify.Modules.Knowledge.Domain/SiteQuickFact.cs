namespace Intentify.Modules.Knowledge.Domain;

public sealed class SiteQuickFact
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string Fact { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
