namespace Intentify.Modules.Knowledge.Application;

public sealed record CreateKnowledgeSourceCommand(Guid TenantId, Guid SiteId, string Type, string? Name, string? Url, string? Text);

public sealed record UploadPdfCommand(Guid TenantId, Guid SourceId, byte[] Bytes);

public sealed record IndexKnowledgeSourceCommand(Guid TenantId, Guid SourceId);

public sealed record RetrieveTopChunksQuery(Guid TenantId, Guid SiteId, string Query, int TopK);

public sealed record CreateKnowledgeSourceResult(Guid SourceId, string Status);

public sealed record IndexKnowledgeSourceResult(string Status, int ChunkCount, string? FailureReason);

public sealed record RetrievedChunkResult(Guid ChunkId, Guid SourceId, int ChunkIndex, string Content, int Score);
