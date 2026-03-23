using Intentify.Modules.Tickets.Domain;

namespace Intentify.Modules.Tickets.Application;

public sealed record CreateTicketCommand(
    Guid TenantId,
    Guid SiteId,
    Guid? VisitorId,
    Guid? EngageSessionId,
    string Subject,
    string Description,
    Guid? AssignedToUserId,
    string? ContactName = null,
    string? PreferredContactMethod = null,
    string? PreferredContactDetail = null,
    string? OpportunityLabel = null,
    int? IntentScore = null,
    string? ConversationSummary = null,
    string? SuggestedFollowUp = null);
public sealed record GetTicketQuery(Guid TenantId, Guid TicketId);
public sealed record ListTicketsQuery(Guid TenantId, Guid? SiteId, Guid? VisitorId, Guid? EngageSessionId, int Page, int PageSize);
public sealed record UpdateTicketCommand(Guid TenantId, Guid TicketId, string Subject, string Description);
public sealed record SetTicketAssignmentCommand(Guid TenantId, Guid TicketId, Guid? AssignedToUserId);
public sealed record AddTicketNoteCommand(Guid TenantId, Guid TicketId, Guid AuthorUserId, string Content);
public sealed record ListTicketNotesQuery(Guid TenantId, Guid TicketId, int Page, int PageSize);
public sealed record TransitionTicketStatusCommand(Guid TenantId, Guid TicketId, string ToStatus, Guid? CurrentUserId);

public sealed record TicketListItem(
    Guid Id,
    Guid SiteId,
    Guid? VisitorId,
    Guid? EngageSessionId,
    string Subject,
    string Status,
    Guid? AssignedToUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public interface ITicketRepository
{
    Task InsertAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task<Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default);
    Task ReplaceAsync(Ticket ticket, CancellationToken cancellationToken = default);
}

public interface ITicketNoteRepository
{
    Task InsertAsync(TicketNote note, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TicketNote>> ListAsync(ListTicketNotesQuery query, CancellationToken cancellationToken = default);
}
