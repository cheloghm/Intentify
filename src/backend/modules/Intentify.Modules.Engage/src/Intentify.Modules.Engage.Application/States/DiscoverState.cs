using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

/// <summary>
/// Handles all non-greeting, non-capture turns.
/// Single responsibility: apply the AI reply, persist captured slots, and update session state.
/// The AI decides what to say — this state never overrides the reply with hardcoded copy.
/// </summary>
public sealed class DiscoverState : IEngageState
{
    public string StateName => "Discover";

    private readonly ResponseShaper _shaper;

    public DiscoverState(ResponseShaper shaper)
    {
        _shaper = shaper;
    }

    public Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var decision = ctx.TurnDecision;
        var action = ctx.PrimaryActionDecision?.Action ?? EngageNextAction.AskDiscoveryQuestion;

        // Persist all slots the AI extracted this turn
        EngageSlotApplicator.Apply(ctx.Session, decision);

        // Update session state based on action signal
        if (action == EngageNextAction.CloseConversation)
        {
            ctx.Session.IsConversationComplete = true;
            ctx.Session.LastCompletedAtUtc = DateTime.UtcNow;
            ctx.Session.PendingCaptureMode = null;
        }
        else if (action == EngageNextAction.EscalateSupport)
        {
            ctx.Session.PendingCaptureMode = "Support";
            ctx.Session.IsConversationComplete = false;
        }
        else if (action == EngageNextAction.AskCaptureQuestion)
        {
            ctx.Session.PendingCaptureMode = "Commercial";
            ctx.Session.IsConversationComplete = false;
        }
        else
        {
            ctx.Session.IsConversationComplete = false;
        }

        ctx.Session.ConversationState = "Discover";

        // The AI reply is the response — no fallback copy.
        // The safety net below handles complete AI failure only (empty reply from invalid decision).
        var rawReply = !string.IsNullOrWhiteSpace(decision.Reply)
            ? decision.Reply
            : "I'm here to help — could you tell me a bit more about what you're looking for?";

        var reply = _shaper.Shape(rawReply, ctx);

        return Task.FromResult(OperationResult<ChatSendResult>.Success(
            new ChatSendResult(ctx.Session.Id, reply, decision.Confidence, false, Array.Empty<EngageCitationResult>(), "Discover")));
    }
}
