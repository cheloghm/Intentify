namespace Intentify.Modules.Knowledge.Domain;

public sealed class KnowledgeChunk
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public Guid SourceId { get; init; }

    public int ChunkIndex { get; init; }

    public string Content { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}
