namespace Intentify.Shared.AI;

public sealed class AiOptions
{
    public string? ApiBaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? ChatModel { get; init; }

    public string? EmbeddingModel { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public int MaxPromptChars { get; init; }
}
