using System.Text;
using System.Text.Json;
using Intentify.Shared.AI;

namespace Intentify.Modules.Engage.Application;

public sealed class Stage7AiDecisionGenerationService
{
    private const decimal LowConfidenceThreshold = 0.5m;


    private static readonly IReadOnlyCollection<AiRecommendationType> DefaultAllowlistedActions = Enum
        .GetValues<AiRecommendationType>()
        .ToArray();

    private readonly IChatCompletionClient _chatCompletionClient;

    public Stage7AiDecisionGenerationService(IChatCompletionClient chatCompletionClient)
    {
        _chatCompletionClient = chatCompletionClient;
    }

    public async Task<AiDecisionContract> GenerateAsync(
        Stage7VisitorContextBundle contextBundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextBundle);

        var prompt = BuildPrompt(contextBundle);
        var completion = await _chatCompletionClient.CompleteAsync(prompt, cancellationToken);

        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            return CreateInvalidNoAction(contextBundle.ContextRef, "AiUnavailable", "AI decision generation is unavailable.");
        }

        if (!TryParseDecision(completion.Value, contextBundle.ContextRef, out var parsedDecision))
        {
            return CreateInvalidNoAction(contextBundle.ContextRef, "MalformedOutput", "AI decision output was malformed.");
        }

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(parsedDecision);
        if (validated.ValidationStatus == AiDecisionValidationStatus.Invalid)
        {
            return validated;
        }

        if (validated.OverallConfidence < LowConfidenceThreshold)
        {
            return CreateSafeNoAction(
                contextBundle.ContextRef,
                validated.OverallConfidence,
                "LowConfidence",
                "Decision confidence is below safe threshold.");
        }

        return validated;
    }

    private static bool TryParseDecision(string rawOutput, AiDecisionContextRef contextRef, out AiDecisionContract decision)
    {
        decision = CreateInvalidNoAction(contextRef, "MalformedOutput", "AI decision output was malformed.");

        var json = TryExtractJson(rawOutput);
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

            var schemaVersion = ReadString(root, "schemaVersion") ?? string.Empty;
            var decisionId = ReadString(root, "decisionId") ?? string.Empty;
            var overallConfidence = ReadDecimal(root, "overallConfidence");
            var shouldFallback = ReadBoolean(root, "shouldFallback");
            var fallbackReason = ReadString(root, "fallbackReason");
            var noActionMessage = ReadString(root, "noActionMessage");

            var recommendations = ParseRecommendations(root);

            decision = new AiDecisionContract(
                schemaVersion,
                decisionId,
                contextRef,
                overallConfidence,
                recommendations,
                AiDecisionValidationStatus.Valid,
                [],
                DefaultAllowlistedActions,
                shouldFallback,
                fallbackReason,
                noActionMessage);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyCollection<AiRecommendation> ParseRecommendations(JsonElement root)
    {
        if (!root.TryGetProperty("recommendations", out var recommendationsElement)
            || recommendationsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var recommendations = new List<AiRecommendation>();

        foreach (var item in recommendationsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var typeName = ReadString(item, "type");
            var recommendationType = Enum.TryParse<AiRecommendationType>(typeName, ignoreCase: true, out var parsed)
                ? parsed
                : (AiRecommendationType)int.MaxValue;

            var evidenceRefs = ParseEvidenceRefs(item);
            var targetRefs = ParseTargetRefs(item);
            var proposedCommand = ParseProposedCommand(item);

            recommendations.Add(new AiRecommendation(
                recommendationType,
                ReadDecimal(item, "confidence"),
                ReadString(item, "rationale") ?? string.Empty,
                evidenceRefs,
                targetRefs,
                ReadBoolean(item, "requiresApproval"),
                proposedCommand));
        }

        return recommendations;
    }

    private static IReadOnlyCollection<AiEvidenceRef>? ParseEvidenceRefs(JsonElement recommendation)
    {
        if (!recommendation.TryGetProperty("evidenceRefs", out var evidenceElement)
            || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<AiEvidenceRef>();
        foreach (var item in evidenceElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new AiEvidenceRef(
                ReadString(item, "source") ?? string.Empty,
                ReadString(item, "referenceId") ?? string.Empty,
                ReadString(item, "detail")));
        }

        return result;
    }

    private static AiTargetRefs? ParseTargetRefs(JsonElement recommendation)
    {
        if (!recommendation.TryGetProperty("targetRefs", out var targetElement)
            || targetElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AiTargetRefs(
            ReadGuid(targetElement, "promoId"),
            ReadString(targetElement, "promoPublicKey"),
            ReadGuid(targetElement, "knowledgeSourceId"),
            ReadGuid(targetElement, "ticketId"),
            ReadGuid(targetElement, "visitorId"));
    }

    private static IReadOnlyDictionary<string, string>? ParseProposedCommand(JsonElement recommendation)
    {
        if (!recommendation.TryGetProperty("proposedCommand", out var commandElement)
            || commandElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var command = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in commandElement.EnumerateObject())
        {
            command[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return command;
    }

    private static AiDecisionContract CreateInvalidNoAction(
        AiDecisionContextRef contextRef,
        string fallbackReason,
        string noActionMessage)
    {
        return new AiDecisionContract(
            SchemaVersion: "stage7.v1",
            DecisionId: Guid.NewGuid().ToString("N"),
            ContextRef: contextRef,
            OverallConfidence: 0m,
            Recommendations:
            [
                new AiRecommendation(
                    AiRecommendationType.NoAction,
                    0m,
                    "Unable to produce a valid decision. Falling back to no-action.",
                    [],
                    null,
                    false,
                    null)
            ],
            ValidationStatus: AiDecisionValidationStatus.Invalid,
            ValidationErrors: ["AI decision output failed validation."],
            AllowlistedActions: DefaultAllowlistedActions,
            ShouldFallback: true,
            FallbackReason: fallbackReason,
            NoActionMessage: noActionMessage);
    }

    private static AiDecisionContract CreateSafeNoAction(
        AiDecisionContextRef contextRef,
        decimal confidence,
        string fallbackReason,
        string noActionMessage)
    {
        return new AiDecisionContract(
            SchemaVersion: "stage7.v1",
            DecisionId: Guid.NewGuid().ToString("N"),
            ContextRef: contextRef,
            OverallConfidence: confidence,
            Recommendations:
            [
                new AiRecommendation(
                    AiRecommendationType.NoAction,
                    confidence,
                    "Confidence is below safe threshold.",
                    [],
                    null,
                    false,
                    null)
            ],
            ValidationStatus: AiDecisionValidationStatus.Valid,
            ValidationErrors: [],
            AllowlistedActions: DefaultAllowlistedActions,
            ShouldFallback: true,
            FallbackReason: fallbackReason,
            NoActionMessage: noActionMessage);
    }

    private static string BuildPrompt(Stage7VisitorContextBundle contextBundle)
    {
        var builder = new StringBuilder();

        builder.AppendLine("You are Intentify Stage 7 AI decision assistant.");
        builder.AppendLine("Output ONLY valid JSON. No markdown, no prose.");
        builder.AppendLine("The JSON must follow this schema exactly:");
        builder.AppendLine("{");
        builder.AppendLine("  \"schemaVersion\": \"stage7.v1\",");
        builder.AppendLine("  \"decisionId\": \"<guid-like-id>\",");
        builder.AppendLine("  \"overallConfidence\": <0..1>,");
        builder.AppendLine("  \"recommendations\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"type\": \"SuggestPromo|SuggestKnowledge|EscalateTicket|TagVisitor|SuggestKnowledgeUpdate|NotifyClientKnowledgeGap|NoAction\",");
        builder.AppendLine("      \"confidence\": <0..1>,");
        builder.AppendLine("      \"rationale\": \"short\",");
        builder.AppendLine("      \"evidenceRefs\": [{\"source\":\"...\",\"referenceId\":\"...\",\"detail\":\"...\"}],");
        builder.AppendLine("      \"targetRefs\": {\"promoId\":\"...\",\"promoPublicKey\":\"...\",\"knowledgeSourceId\":\"...\",\"ticketId\":\"...\",\"visitorId\":\"...\"},");
        builder.AppendLine("      \"requiresApproval\": true|false,");
        builder.AppendLine("      \"proposedCommand\": {\"key\":\"value\"}");
        builder.AppendLine("    }");
        builder.AppendLine("  ],");
        builder.AppendLine("  \"shouldFallback\": true|false,");
        builder.AppendLine("  \"fallbackReason\": \"...\",");
        builder.AppendLine("  \"noActionMessage\": \"...\"");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- If uncertain, return NoAction with shouldFallback=true.");
        builder.AppendLine("- Do not propose action execution steps.");
        builder.AppendLine("- Keep recommendations to max 3.");
        builder.AppendLine();
        builder.AppendLine("Context:");
        builder.AppendLine($"tenantId: {contextBundle.ContextRef.TenantId:D}");
        builder.AppendLine($"siteId: {contextBundle.ContextRef.SiteId:D}");
        builder.AppendLine($"visitorId: {(contextBundle.ContextRef.VisitorId?.ToString("D") ?? "null")}");
        builder.AppendLine($"engageSessionId: {(contextBundle.ContextRef.EngageSessionId?.ToString("D") ?? "null")}");
        builder.AppendLine($"knowledgeQuery: {contextBundle.KnowledgeRetrievalSnapshot.Query}");

        foreach (var chunk in contextBundle.KnowledgeRetrievalSnapshot.TopChunks.Take(3))
        {
            builder.AppendLine($"knowledgeChunk: sourceId={chunk.SourceId:D}; chunkId={chunk.ChunkId:D}; score={chunk.Score}; excerpt={chunk.ContentExcerpt}");
        }

        if (contextBundle.RecentEngageSummary is { } engageSummary)
        {
            builder.AppendLine($"engageSummary: messages={engageSummary.Messages.Count}");
            foreach (var message in engageSummary.Messages.TakeLast(6))
            {
                builder.AppendLine($"engageMessage: role={message.Role}; confidence={(message.Confidence?.ToString() ?? "null")}; excerpt={message.ContentExcerpt}");
            }
        }

        if (contextBundle.LinkedTicketsSummary is { Count: > 0 } tickets)
        {
            foreach (var ticket in tickets.Take(5))
            {
                builder.AppendLine($"ticket: id={ticket.TicketId:D}; status={ticket.Status}; subject={ticket.Subject}");
            }
        }

        if (contextBundle.PromoInteractionSummary is { Count: > 0 } promos)
        {
            foreach (var promo in promos.Take(5))
            {
                builder.AppendLine($"promoEntry: id={promo.PromoEntryId:D}; promoId={promo.PromoId:D}; submittedAt={promo.SubmittedAtUtc:O}");
            }
        }

        return builder.ToString();
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

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0m;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static Guid? ReadGuid(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }
}
