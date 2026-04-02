using Intentify.Shared.Abstractions;

namespace Intentify.Shared.AI;

public interface IChatCompletionClient
{
    Task<Result<string>> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}
