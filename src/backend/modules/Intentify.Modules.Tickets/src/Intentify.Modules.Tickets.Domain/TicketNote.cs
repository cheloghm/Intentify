namespace Intentify.Modules.Tickets.Domain;

public sealed class TicketNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid TicketId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
