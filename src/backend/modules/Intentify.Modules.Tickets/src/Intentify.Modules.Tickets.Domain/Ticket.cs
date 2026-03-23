namespace Intentify.Modules.Tickets.Domain;

public sealed class Ticket
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public Guid? VisitorId { get; set; }
    public Guid? EngageSessionId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? PreferredContactDetail { get; set; }
    public string? OpportunityLabel { get; set; }
    public int? IntentScore { get; set; }
    public string? ConversationSummary { get; set; }
    public string? SuggestedFollowUp { get; set; }
    public string Status { get; set; } = TicketStatuses.Open;
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class TicketStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly ISet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        Open,
        InProgress,
        Resolved,
        Closed
    };
}
