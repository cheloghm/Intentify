namespace Intentify.Modules.Engage.Domain;

public sealed class EngageChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public decimal? Confidence { get; init; }

    public IReadOnlyCollection<EngageCitation>? Citations { get; init; }
}
