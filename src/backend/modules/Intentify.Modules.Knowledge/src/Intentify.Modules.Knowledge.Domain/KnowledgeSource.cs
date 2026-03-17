namespace Intentify.Modules.Knowledge.Domain;

public sealed class KnowledgeSource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public Guid BotId { get; init; }

    public string Type { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Url { get; set; }

    public string? TextContent { get; set; }

    public byte[]? PdfBytes { get; set; }

    public IndexStatus Status { get; set; } = IndexStatus.Queued;

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? IndexedAtUtc { get; set; }

    public int ChunkCount { get; set; }
}
