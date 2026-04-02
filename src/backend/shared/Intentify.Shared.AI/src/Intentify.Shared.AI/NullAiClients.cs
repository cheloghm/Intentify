using Intentify.Shared.Abstractions;

namespace Intentify.Shared.AI;

public sealed class NullChatCompletionClient(AiOptions options) : IChatCompletionClient
{
    public Task<Result<string>> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(CreateNotConfiguredError(options)));

    private static Error CreateNotConfiguredError(AiOptions options)
        => string.IsNullOrWhiteSpace(options.ApiBaseUrl)
            ? new Error("AI_NOT_CONFIGURED", "AI ApiBaseUrl is not configured.")
            : new Error("AI_NOT_CONFIGURED", "No AI chat completion provider is configured.");
}

public sealed class NullEmbeddingClient(AiOptions options) : IEmbeddingClient
{
    public Task<Result<float[]>> EmbedAsync(string input, CancellationToken ct)
        => Task.FromResult(Result<float[]>.Failure(CreateNotConfiguredError(options)));

    private static Error CreateNotConfiguredError(AiOptions options)
        => string.IsNullOrWhiteSpace(options.ApiBaseUrl)
            ? new Error("AI_NOT_CONFIGURED", "AI ApiBaseUrl is not configured.")
            : new Error("AI_NOT_CONFIGURED", "No AI embedding provider is configured.");
}
