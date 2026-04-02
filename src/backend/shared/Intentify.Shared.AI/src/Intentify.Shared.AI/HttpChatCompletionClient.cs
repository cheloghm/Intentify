using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.Shared.Abstractions;

namespace Intentify.Shared.AI;

public sealed class HttpChatCompletionClient(AiOptions options, HttpClient httpClient) : IChatCompletionClient
{
    public const string ClientName = "intentify-ai-chat";

    public async Task<Result<string>> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return Result<string>.Failure(new Error("AI_NOT_CONFIGURED", "AI chat completion provider credentials are not configured."));
        }

        try
        {
            var isAnthropic = options.ApiBaseUrl.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase);

            object systemContent = isAnthropic
                ? new object[] { new { type = "text", text = systemPrompt, cache_control = new { type = "ephemeral" } } }
                : (object)systemPrompt;

            var client = httpClient;
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = JsonContent.Create(new
                {
                    model = options.ChatModel,
                    temperature = 0,
                    messages = new object[]
                    {
                        new { role = "system", content = systemContent },
                        new { role = "user",   content = userPrompt }
                    }
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            if (isAnthropic)
                request.Headers.TryAddWithoutValidation("anthropic-beta", "prompt-caching-2024-07-31");

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure(new Error("AI_UNAVAILABLE", "AI chat completion request failed."));
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var payload = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);

            var choices = payload.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned no choices."));
            }

            var message = choices[0].GetProperty("message");
            var text = message.GetProperty("content").GetString();

            return string.IsNullOrWhiteSpace(text)
                ? Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned empty content."))
                : Result<string>.Success(text);
        }
        catch (JsonException)
        {
            return Result<string>.Failure(new Error("AI_INVALID_RESPONSE", "AI chat completion returned an invalid payload."));
        }
        catch (HttpRequestException)
        {
            return Result<string>.Failure(new Error("AI_UNAVAILABLE", "AI chat completion request failed."));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("AI_TIMEOUT", "AI chat completion request timed out."));
        }
    }
}
