namespace Intentify.Modules.Knowledge.Api;

public sealed record CreateKnowledgeSourceRequest(string SiteId, string Type, string? Name, string? Url, string? Text);

public sealed record CreateKnowledgeSourceResponse(string SourceId, string Status);

public sealed record IndexKnowledgeSourceResponse(string Status, int ChunkCount, string? FailureReason);

public sealed record KnowledgeSourceSummaryResponse(
    string SourceId,
    string SiteId,
    string Type,
    string? Name,
    string? Url,
    string Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? IndexedAtUtc);

public sealed record RetrieveChunkResponse(string ChunkId, string SourceId, int ChunkIndex, string Content, int Score);
