namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed record OpenSearchChunkDocument(
    Guid ChunkId,
    int ChunkIndex,
    string Content);
