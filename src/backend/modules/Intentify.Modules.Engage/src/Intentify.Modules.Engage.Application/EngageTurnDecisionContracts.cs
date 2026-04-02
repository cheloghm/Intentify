namespace Intentify.Modules.Engage.Application;

/// <summary>
/// The structured slots captured from a single conversation turn.
/// All fields are nullable — the AI only populates what it explicitly extracted.
/// Values must not be invented or guessed; they are only set when clearly stated or clearly implied.
/// </summary>
public sealed record EngageTurnSlots(
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    string? Location = null,
    string? Goal = null,
    string? Type = null,
    string? Timeline = null,
    string? Budget = null,
    string? Constraints = null,
    string? DecisionStage = null);

/// <summary>
/// The AI's complete decision for a single conversation turn.
/// Replaces the multi-recommendation AiDecisionContract with one clean, unambiguous output per turn.
/// The reply field is the only thing the visitor will see — it is the ground truth response.
/// </summary>
public sealed record EngageTurnDecision(
    string Reply,
    string Intent,
    EngageTurnSlots CapturedSlots,
    bool CreateLead,
    bool CreateTicket,
    string? TicketSubject,
    string? TicketSummary,
    string? SuggestedFollowUp,
    bool ConversationComplete,
    decimal Confidence,
    bool IsValid,
    string? FallbackReason);
