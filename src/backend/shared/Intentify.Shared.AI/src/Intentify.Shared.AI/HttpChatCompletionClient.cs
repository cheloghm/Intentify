using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace Intentify.Shared.AI;

public sealed class HttpChatCompletionClient(AiOptions options, HttpClient httpClient, ILogger<HttpChatCompletionClient>? logger = null) : IChatCompletionClient
{
    public const string ClientName = "intentify-ai-chat";

    public async Task<Result<string>> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return Result<string>.Failure(new Error("AI_NOT_CONFIGURED", "AI chat completion provider credentials are not configured."));
        }

        if (options.MaxPromptChars > 0 && userPrompt.Length > options.MaxPromptChars)
            userPrompt = userPrompt[..options.MaxPromptChars];

        try
        {
            var isAnthropic = options.ApiBaseUrl.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase);

            if (isAnthropic)
            {
                // ── Native Anthropic Messages API ────────────────────────
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = JsonContent.Create(new
                    {
                        model = options.ChatModel,
                        max_tokens = 2000,
                        temperature = 0,
                        system = new object[]
                        {
                            new { type = "text", text = systemPrompt, cache_control = new { type = "ephemeral" } }
                        },
                        messages = new object[]
                        {
                            new { role = "user", content = userPrompt }
                        }
                    })
                };
                request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                request.Headers.TryAddWithoutValidation("anthropic-beta", "prompt-caching-2024-07-31");

                using var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return Result<string>.Failure(new Error("AI_UNAVAILABLE", "AI chat completion request failed."));

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var payload = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);

                LogAnthropicUsage(payload.RootElement);

                // Native format: { "content": [{ "type": "text", "text": "..." }] }
                var content = payload.RootElement.GetProperty("content");
                if (content.GetArrayLength() == 0)
                    return Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned no choices."));

                var text = content[0].GetProperty("text").GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned empty content."))
                    : Result<string>.Success(text);
            }
            else
            {
                // ── OpenAI-compatible path (unchanged) ───────────────────
                using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
                {
                    Content = JsonContent.Create(new
                    {
                        model = options.ChatModel,
                        temperature = 0,
                        messages = new object[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user",   content = userPrompt }
                        }
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

                using var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return Result<string>.Failure(new Error("AI_UNAVAILABLE", "AI chat completion request failed."));

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var payload = await JsonDocument.ParseAsync(contentStream, cancellationToken: ct);

                var choices = payload.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                    return Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned no choices."));

                var message = choices[0].GetProperty("message");
                var text = message.GetProperty("content").GetString();

                return string.IsNullOrWhiteSpace(text)
                    ? Result<string>.Failure(new Error("AI_EMPTY", "AI chat completion returned empty content."))
                    : Result<string>.Success(text);
            }
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

    private void LogAnthropicUsage(JsonElement root)
    {
        if (logger is null) return;
        if (!root.TryGetProperty("usage", out var usage)) return;

        var inputTokens       = ReadInt(usage, "input_tokens");
        var outputTokens      = ReadInt(usage, "output_tokens");
        var cacheRead         = ReadInt(usage, "cache_read_input_tokens");
        var cacheCreation     = ReadInt(usage, "cache_creation_input_tokens");

        logger.LogInformation(
            "Anthropic usage — input={InputTokens} output={OutputTokens} cache_read={CacheRead} cache_creation={CacheCreation} model={Model}",
            inputTokens, outputTokens, cacheRead, cacheCreation, options.ChatModel);
    }

    private static int ReadInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
}
