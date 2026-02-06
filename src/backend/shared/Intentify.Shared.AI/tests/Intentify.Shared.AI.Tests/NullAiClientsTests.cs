namespace Intentify.Shared.AI.Tests;

public sealed class NullAiClientsTests
{
    [Fact]
    public async Task ChatClient_WhenApiBaseUrlMissing_ReturnsControlledFailure()
    {
        var client = new NullChatCompletionClient(new AiOptions());

        var result = await client.CompleteAsync("prompt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("AI_NOT_CONFIGURED", result.Error!.Value.Code);
    }

    [Fact]
    public async Task EmbeddingClient_WhenApiBaseUrlMissing_ReturnsControlledFailure()
    {
        var client = new NullEmbeddingClient(new AiOptions());

        var result = await client.EmbedAsync("input", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("AI_NOT_CONFIGURED", result.Error!.Value.Code);
    }
}
