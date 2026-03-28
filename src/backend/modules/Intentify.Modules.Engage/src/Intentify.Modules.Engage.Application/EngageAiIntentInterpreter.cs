using System.Text;
using System.Text.Json;
using Intentify.Modules.Engage.Domain;
using Intentify.Shared.AI;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageAiIntentInterpreter
{
    private readonly IChatCompletionClient _chatCompletionClient;

    public EngageAiIntentInterpreter(IChatCompletionClient chatCompletionClient)
    {
        _chatCompletionClient = chatCompletionClient;
    }

    public async Task<AiIntentInterpretationResult?> InterpretAsync(
        string userMessage,
        string normalizedMessage,
        EngageChatSession session,
        IReadOnlyCollection<string> tenantVocabulary,
        string? businessContext,
        string? botTone,
        string? botVerbosity,
        string? botFallbackStyle,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(userMessage, normalizedMessage, session, tenantVocabulary, businessContext, botTone, botVerbosity, botFallbackStyle);
        var completion = await _chatCompletionClient.CompleteAsync(prompt, cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            return null;
        }

        return TryParse(completion.Value, out var parsed) ? parsed : null;
    }

    private static string BuildPrompt(
        string userMessage,
        string normalizedMessage,
        EngageChatSession session,
        IReadOnlyCollection<string> tenantVocabulary,
        string? businessContext,
        string? botTone,
        string? botVerbosity,
        string? botFallbackStyle)
    {
        var vocabulary = string.Join(", ", tenantVocabulary.Take(60));

        var contextSignals = new[]
        {
            session.CaptureGoal is { Length: > 0 } ? $"goal={session.CaptureGoal}" : null,
            session.CaptureType is { Length: > 0 } ? $"type={session.CaptureType}" : null,
            session.CaptureLocation is { Length: > 0 } ? $"location={session.CaptureLocation}" : null,
            session.CaptureConstraints is { Length: > 0 } ? $"constraints={session.CaptureConstraints}" : null
        }.Where(item => item is not null);

        var builder = new StringBuilder();
        builder.AppendLine("You classify website chat intent.");
        builder.AppendLine("Return JSON only.");
        builder.AppendLine("Schema:");
        builder.AppendLine("{\"intent\":\"General|Contact|Location|Hours|Services|Organization|EscalationHelp|AmbiguousShortPrompt\",\"confidence\":0.0,\"rationale\":\"short\"}");
        builder.AppendLine("Rules:");
        builder.AppendLine("- EscalationHelp only for explicit human/support/handoff requests or urgent support issues.");
        builder.AppendLine("- Do not invent entities.");
        builder.AppendLine("- Confidence between 0 and 1.");
        builder.AppendLine($"Tenant vocabulary: {(string.IsNullOrWhiteSpace(vocabulary) ? "none" : vocabulary)}");
        builder.AppendLine($"Business context bundle: {(string.IsNullOrWhiteSpace(businessContext) ? "none" : businessContext)}");
        builder.AppendLine($"Bot persona config: tone={(string.IsNullOrWhiteSpace(botTone) ? "default" : botTone)}, verbosity={(string.IsNullOrWhiteSpace(botVerbosity) ? "default" : botVerbosity)}, fallbackStyle={(string.IsNullOrWhiteSpace(botFallbackStyle) ? "default" : botFallbackStyle)}");
        builder.AppendLine($"Session capture context: {(contextSignals.Any() ? string.Join("; ", contextSignals) : "none")}");
        builder.AppendLine($"User message: {userMessage}");
        builder.AppendLine($"Normalized user message: {normalizedMessage}");

        return builder.ToString();
    }

    private static bool TryParse(string raw, out AiIntentInterpretationResult parsed)
    {
        parsed = default;

        var json = TryExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var intentValue = root.TryGetProperty("intent", out var intentElement) && intentElement.ValueKind == JsonValueKind.String
                ? intentElement.GetString()
                : null;

            var confidence = root.TryGetProperty("confidence", out var confidenceElement)
                ? ReadDecimal(confidenceElement)
                : 0m;

            var rationale = root.TryGetProperty("rationale", out var rationaleElement) && rationaleElement.ValueKind == JsonValueKind.String
                ? rationaleElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(intentValue)
                || !Enum.TryParse<ChatIntent>(intentValue, ignoreCase: true, out var intent)
                || !Enum.IsDefined(intent)
                || confidence < 0m
                || confidence > 1m)
            {
                return false;
            }

            parsed = new AiIntentInterpretationResult(intent, confidence, rationale);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static decimal ReadDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static string? TryExtractJson(string rawOutput)
    {
        var trimmed = rawOutput.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return null;
    }
}

public sealed record AiIntentInterpretationResult
{
    internal AiIntentInterpretationResult(ChatIntent intent, decimal confidence, string rationale)
    {
        Intent = intent;
        Confidence = confidence;
        Rationale = rationale;
    }

    internal ChatIntent Intent { get; }

    internal decimal Confidence { get; }

    internal string Rationale { get; }
}
