namespace Intentify.Modules.Leads.Domain;

public sealed class Lead
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string? PrimaryEmail { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public Guid? LinkedVisitorId { get; set; }
    public string? FirstPartyId { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
}
