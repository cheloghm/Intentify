namespace Intentify.Modules.Engage.Domain;

public sealed class EngageCitation
{
    public Guid SourceId { get; init; }

    public Guid ChunkId { get; init; }

    public int ChunkIndex { get; init; }
}
