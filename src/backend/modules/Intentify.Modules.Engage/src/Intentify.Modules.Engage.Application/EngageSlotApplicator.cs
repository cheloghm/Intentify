using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

/// <summary>
/// Applies AI-extracted slots from a turn decision to the session entity.
/// Only applies non-null, non-whitespace values — existing session fields are never overwritten with blanks.
/// </summary>
internal static class EngageSlotApplicator
{
    internal static void Apply(EngageChatSession session, EngageTurnDecision decision)
    {
        var slots = decision.CapturedSlots;

        if (!string.IsNullOrWhiteSpace(slots.Name))     session.CapturedName = slots.Name.Trim();
        if (!string.IsNullOrWhiteSpace(slots.Email))    session.CapturedEmail = slots.Email.Trim();
        if (!string.IsNullOrWhiteSpace(slots.Phone))    session.CapturedPhone = slots.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(slots.Location)) session.CaptureLocation = slots.Location.Trim();
        if (!string.IsNullOrWhiteSpace(slots.Goal))     session.CaptureGoal = slots.Goal.Trim();
        if (!string.IsNullOrWhiteSpace(slots.Type))     session.CaptureType = slots.Type.Trim();

        // Constraints is the catch-all for timeline, budget, and requirement constraints
        var constraintParts = new[] { slots.Constraints, slots.Timeline, slots.Budget }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToArray();
        if (constraintParts.Length > 0)
            session.CaptureConstraints = string.Join("; ", constraintParts);

        if (!string.IsNullOrWhiteSpace(slots.DecisionStage))
            session.OpportunityLabel = slots.DecisionStage.Trim();

        if (!string.IsNullOrWhiteSpace(decision.SuggestedFollowUp))
            session.SuggestedFollowUp = decision.SuggestedFollowUp.Trim();

        if (!string.IsNullOrWhiteSpace(decision.Intent))
            session.ConversationSummary = decision.Intent.Trim();
    }
}
