namespace Intentify.Modules.Engage.Domain;

public sealed class EngageHandoffTicket
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public Guid SessionId { get; init; }

    public string UserMessage { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string? LastAssistantMessage { get; init; }

    public string? TranscriptExcerpt { get; init; }

    public int CitationCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
