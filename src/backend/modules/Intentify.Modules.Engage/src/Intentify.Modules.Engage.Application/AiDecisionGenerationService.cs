using System.Text;
using System.Text.Json;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Engage.Application.PhraseBanks;
using Intentify.Shared.AI;

namespace Intentify.Modules.Engage.Application;

public sealed class AiDecisionGenerationService
{
    private const decimal LowConfidenceThreshold = 0.5m;

    private readonly IChatCompletionClient _chatCompletionClient;

    public AiDecisionGenerationService(IChatCompletionClient chatCompletionClient)
    {
        _chatCompletionClient = chatCompletionClient;
    }

    public async Task<EngageTurnDecision> GenerateAsync(
        VisitorContextBundle contextBundle,
        EngageBot bot,
        IReadOnlyCollection<string> tenantVocabulary,
        EngageSessionMemorySnapshot sessionMemory,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextBundle);
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(sessionMemory);

        var prompt = BuildPrompt(contextBundle, bot, tenantVocabulary, sessionMemory, recentMessages);
        var completion = await _chatCompletionClient.CompleteAsync(prompt, cancellationToken);

        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
            return CreateFallback("AiUnavailable", "AI is unavailable.");

        if (!TryParseDecision(completion.Value, out var parsed))
            return CreateFallback("MalformedOutput", "AI output could not be parsed.");

        var validated = AiDecisionValidator.ValidateAndNormalize(parsed);
        if (!validated.IsValid)
            return validated;

        if (validated.Confidence < LowConfidenceThreshold)
            return validated with { FallbackReason = "LowConfidence" };

        return validated;
    }

    private static string BuildPrompt(
        VisitorContextBundle contextBundle,
        EngageBot bot,
        IReadOnlyCollection<string> tenantVocabulary,
        EngageSessionMemorySnapshot sessionMemory,
        IReadOnlyCollection<EngageChatMessage> recentMessages)
    {
        var sb = new StringBuilder();

        // Assemble business briefing from available runtime data
        var briefing = new EngageBriefingContext(
            BusinessName: bot.Name ?? bot.DisplayName,
            Industry: contextBundle.IntelligenceSnapshot?.Category,
            ServicesDescription: tenantVocabulary.Count > 0
                ? string.Join(", ", tenantVocabulary.Take(12))
                : null,
            GeoFocus: contextBundle.IntelligenceSnapshot?.Location,
            Tone: bot.Tone,
            PersonalityDescriptor: null);   // derived from tone inside EngageSalesGuideline

        // Layers 1–6 briefing + current session state
        sb.AppendLine(EngageSalesGuideline.Build(briefing, sessionMemory));

        // Signal pattern examples for AI intent recognition
        sb.AppendLine(EngageSignalExamples.BuildSection());

        // --- Turn context ---
        sb.AppendLine("## Turn Context");
        sb.AppendLine();

        if (contextBundle.VisitorProfile is { } profile)
        {
            sb.AppendLine($"Visitor: {(string.IsNullOrWhiteSpace(profile.DisplayName) ? "anonymous" : profile.DisplayName)}, " +
                          $"{profile.VisitCount} prior visit(s), first seen {profile.FirstSeenAtUtc:yyyy-MM-dd}.");
        }

        // Full conversation history — everything in the session, oldest first
        if (recentMessages.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Conversation History (oldest first)");
            foreach (var msg in recentMessages.OrderBy(m => m.CreatedAtUtc))
            {
                var label = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase)
                    ? "Visitor"
                    : "Assistant";
                sb.AppendLine($"[{label}]: {msg.Content}");
            }
        }

        // Knowledge base — quick facts first, then relevant chunks
        sb.AppendLine();
        sb.AppendLine("### Knowledge Base");

        if (contextBundle.QuickFacts is { Count: > 0 } quickFacts)
        {
            sb.AppendLine("#### Quick Facts (pre-extracted from indexed sources)");
            foreach (var facts in quickFacts)
            {
                if (!string.IsNullOrWhiteSpace(facts.ServicesOffered))
                    sb.AppendLine($"Services offered: {facts.ServicesOffered}");
                if (!string.IsNullOrWhiteSpace(facts.PricingSignals))
                    sb.AppendLine($"Pricing signals: {facts.PricingSignals}");
                if (!string.IsNullOrWhiteSpace(facts.LocationCoverage))
                    sb.AppendLine($"Location / coverage: {facts.LocationCoverage}");
                if (!string.IsNullOrWhiteSpace(facts.HoursAvailability))
                    sb.AppendLine($"Hours / availability: {facts.HoursAvailability}");
                if (!string.IsNullOrWhiteSpace(facts.TeamCredentials))
                    sb.AppendLine($"Team / credentials: {facts.TeamCredentials}");
                if (!string.IsNullOrWhiteSpace(facts.UniqueSellingPoints))
                    sb.AppendLine($"Unique selling points: {facts.UniqueSellingPoints}");
                if (!string.IsNullOrWhiteSpace(facts.FaqsText))
                    sb.AppendLine($"FAQs:\n{facts.FaqsText}");
            }
            sb.AppendLine();
        }

        if (contextBundle.KnowledgeRetrievalSnapshot.TopChunks.Count > 0)
        {
            sb.AppendLine($"#### Relevant chunks for query: \"{contextBundle.KnowledgeRetrievalSnapshot.Query}\"");
            sb.AppendLine();
            foreach (var chunk in contextBundle.KnowledgeRetrievalSnapshot.TopChunks)
            {
                sb.AppendLine($"[Source {chunk.SourceId:D}, chunk {chunk.ChunkIndex}, relevance score {chunk.Score}]");
                sb.AppendLine(chunk.ContentExcerpt);
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No matching knowledge chunks found for this query.");
            sb.AppendLine("If the visitor is asking about specifics, acknowledge honestly that you cannot confirm the details.");
        }

        // Open support tickets linked to this visitor
        if (contextBundle.LinkedTicketsSummary is { Count: > 0 } tickets)
        {
            sb.AppendLine();
            sb.AppendLine("### Open Support Tickets");
            foreach (var ticket in tickets)
            {
                sb.AppendLine($"- [{ticket.Status}] {ticket.Subject} (ticket {ticket.TicketId:D})");
            }
        }

        // Previous promo interactions
        if (contextBundle.PromoInteractionSummary is { Count: > 0 } promos)
        {
            sb.AppendLine();
            sb.AppendLine("### Previous Promo Interactions");
            foreach (var promo in promos)
            {
                var name = string.IsNullOrWhiteSpace(promo.Name) ? string.Empty : $" — {promo.Name}";
                sb.AppendLine($"- Promo submitted {promo.SubmittedAtUtc:yyyy-MM-dd}{name}");
            }
        }

        // JSON output schema
        sb.AppendLine();
        sb.AppendLine("## Required Output");
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY valid JSON. No markdown, no prose, no explanation before or after.");
        sb.AppendLine("The JSON must follow this schema exactly:");
        sb.AppendLine("""
            {
              "reply": "the response to send to the visitor — final, natural, customer-facing text",
              "intent": "one sentence describing what the visitor is doing this turn",
              "capturedSlots": {
                "name": "string or null",
                "email": "string or null",
                "phone": "string or null",
                "location": "string or null",
                "goal": "string or null",
                "type": "string or null",
                "timeline": "string or null",
                "budget": "string or null",
                "constraints": "string or null",
                "decisionStage": "exploring | evaluating | deciding | null"
              },
              "createLead": false,
              "createTicket": false,
              "ticketSummary": "string or null — required when createTicket is true",
              "suggestedFollowUp": "string or null — short internal note for the follow-up team",
              "conversationComplete": false,
              "confidence": 0.9
            }
            """);
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- reply is mandatory. It is the only thing the visitor sees. Make it count.");
        sb.AppendLine("- Only populate capturedSlots fields that were explicitly stated or clearly implied this turn.");
        sb.AppendLine("  Do not invent or guess values. An empty string is not the same as null — use null.");
        sb.AppendLine("- Set createLead = true when you have: name + a contact method + a meaningful goal.");
        sb.AppendLine("- Set createTicket = true when the visitor needs human follow-up and you have enough context.");
        sb.AppendLine("- Set conversationComplete = true when the visitor has what they need and is ready to close.");
        sb.AppendLine("- confidence is your self-assessed reliability (0.0 to 1.0).");
        sb.AppendLine("  Use 0.5 or below if you are uncertain, knowledge is sparse, or the query is out of scope.");

        return sb.ToString();
    }

    private static bool TryParseDecision(string rawOutput, out EngageTurnDecision decision)
    {
        decision = CreateFallback("MalformedOutput", "AI decision output was malformed.");

        var json = TryExtractJson(rawOutput);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var reply = ReadString(root, "reply");
            if (string.IsNullOrWhiteSpace(reply))
                return false;

            decision = new EngageTurnDecision(
                Reply: reply,
                Intent: ReadString(root, "intent") ?? string.Empty,
                CapturedSlots: ReadSlots(root),
                CreateLead: ReadBoolean(root, "createLead"),
                CreateTicket: ReadBoolean(root, "createTicket"),
                TicketSummary: ReadString(root, "ticketSummary"),
                SuggestedFollowUp: ReadString(root, "suggestedFollowUp"),
                ConversationComplete: ReadBoolean(root, "conversationComplete"),
                Confidence: ReadDecimal(root, "confidence"),
                IsValid: true,
                FallbackReason: null);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static EngageTurnSlots ReadSlots(JsonElement root)
    {
        if (!root.TryGetProperty("capturedSlots", out var slotsElement)
            || slotsElement.ValueKind != JsonValueKind.Object)
        {
            return new EngageTurnSlots();
        }

        return new EngageTurnSlots(
            Name: ReadString(slotsElement, "name"),
            Email: ReadString(slotsElement, "email"),
            Phone: ReadString(slotsElement, "phone"),
            Location: ReadString(slotsElement, "location"),
            Goal: ReadString(slotsElement, "goal"),
            Type: ReadString(slotsElement, "type"),
            Timeline: ReadString(slotsElement, "timeline"),
            Budget: ReadString(slotsElement, "budget"),
            Constraints: ReadString(slotsElement, "constraints"),
            DecisionStage: ReadString(slotsElement, "decisionStage"));
    }

    private static EngageTurnDecision CreateFallback(string reason, string message) =>
        new(Reply: string.Empty,
            Intent: "unknown",
            CapturedSlots: new EngageTurnSlots(),
            CreateLead: false,
            CreateTicket: false,
            TicketSummary: null,
            SuggestedFollowUp: null,
            ConversationComplete: false,
            Confidence: 0m,
            IsValid: false,
            FallbackReason: reason);

    private static string? TryExtractJson(string rawOutput)
    {
        var trimmed = rawOutput.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed[start..(end + 1)];

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0m;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
            return parsed;

        return 0m;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }
}
