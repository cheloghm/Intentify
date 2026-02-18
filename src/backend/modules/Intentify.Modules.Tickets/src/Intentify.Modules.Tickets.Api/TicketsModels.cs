namespace Intentify.Modules.Tickets.Api;

public sealed record CreateTicketRequest(Guid SiteId, Guid? VisitorId, Guid? EngageSessionId, string Subject, string Description, Guid? AssignedToUserId);
public sealed record UpdateTicketRequest(string Subject, string Description);
public sealed record SetTicketAssignmentRequest(Guid? AssignedToUserId);
public sealed record AddTicketNoteRequest(string Content);
public sealed record TransitionTicketStatusRequest(string Status);
