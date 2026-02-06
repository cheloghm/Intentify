using Intentify.Shared.Abstractions;

namespace Intentify.Shared.AI;

public interface IEmbeddingClient
{
    Task<Result<float[]>> EmbedAsync(string input, CancellationToken ct);
}
