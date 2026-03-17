using System.Net;
using System.Text;
using System.Text.Json;

namespace Intentify.Shared.AI.Tests;

public sealed class HttpChatCompletionClientTests
{
    [Fact]
    public async Task CompleteAsync_UsesConfiguredChatModelInRequestPayload()
    {
        const string configuredModel = "model-from-config";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "choices": [
                    {
                      "message": {
                        "content": "ok"
                      }
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var client = new HttpChatCompletionClient(new AiOptions
        {
            ApiBaseUrl = "https://example.test",
            ApiKey = "test-key",
            ChatModel = configuredModel
        }, httpClient);

        var result = await client.CompleteAsync("hello", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequestBody);

        using var payload = JsonDocument.Parse(handler.LastRequestBody);
        var model = payload.RootElement.GetProperty("model").GetString();
        Assert.Equal(configuredModel, model);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }
}
