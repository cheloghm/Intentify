namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed record OpenSearchChunkDocument(
    Guid SourceId,
    Guid ChunkId,
    int ChunkIndex,
    string Content);
