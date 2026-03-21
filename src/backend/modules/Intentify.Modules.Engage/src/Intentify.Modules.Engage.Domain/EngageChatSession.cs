namespace Intentify.Modules.Engage.Domain;

public sealed class EngageChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public Guid BotId { get; init; }

    public string WidgetKey { get; init; } = string.Empty;

    public string? CollectorSessionId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; set; }

    public string? ConversationState { get; set; }

    public string? PendingCaptureMode { get; set; }

    public string? CaptureGoal { get; set; }

    public string? CaptureContext { get; set; }

    public string? CaptureType { get; set; }

    public string? CaptureLocation { get; set; }

    public string? CaptureConstraints { get; set; }

    public string? CapturedName { get; set; }

    public string? CapturedEmail { get; set; }

    public string? CapturedPhone { get; set; }
}
