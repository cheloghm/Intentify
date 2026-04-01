namespace Intentify.Modules.Engage.Application;

public static class AiDecisionValidator
{
    public const string DefaultFallbackReason = "ValidationFailed";

    private static readonly string[] AllowedDecisionStageValues = ["exploring", "evaluating", "deciding"];

    // Claims that must never appear in visible or persisted text
    private static readonly string[] UnsafeTerms = ["guarantee", "100%", "wire transfer", "send payment"];

    public static EngageTurnDecision ValidateAndNormalize(EngageTurnDecision decision)
    {
        if (!decision.IsValid)
            return decision;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(decision.Reply))
            errors.Add("reply is required and must not be empty.");

        if (decision.Reply is { Length: > 1200 })
            errors.Add("reply exceeds maximum length of 1200 characters.");

        if (decision.Confidence < 0m || decision.Confidence > 1m)
            errors.Add($"confidence must be between 0 and 1 (got {decision.Confidence}).");

        if (decision.CreateTicket && string.IsNullOrWhiteSpace(decision.TicketSummary))
            errors.Add("ticketSummary is required when createTicket is true.");

        if (decision.TicketSummary is { Length: > 3000 })
            errors.Add("ticketSummary exceeds maximum length of 3000 characters.");

        if (decision.CapturedSlots?.DecisionStage is { } stage
            && !string.IsNullOrWhiteSpace(stage)
            && !AllowedDecisionStageValues.Contains(stage, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"capturedSlots.decisionStage must be one of: {string.Join(", ", AllowedDecisionStageValues)} (got \"{stage}\").");
        }

        ValidateSafety(decision.Reply, "reply", errors);

        if (decision.TicketSummary is not null)
            ValidateSafety(decision.TicketSummary, "ticketSummary", errors);

        if (errors.Count > 0)
            return decision with { IsValid = false, FallbackReason = DefaultFallbackReason };

        return decision;
    }

    private static void ValidateSafety(string text, string fieldName, List<string> errors)
    {
        var normalized = text.ToLowerInvariant();
        foreach (var term in UnsafeTerms)
        {
            if (normalized.Contains(term, StringComparison.Ordinal))
                errors.Add($"{fieldName} contains an unsafe claim: \"{term}\".");
        }
    }
}
