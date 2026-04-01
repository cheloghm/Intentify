using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

/// <summary>
/// Thin slot-persistence layer for commercial capture turns.
/// The AI decides what to ask next based on what is missing and what feels natural —
/// this state applies the reply and persists whatever the AI extracted.
/// No field-by-field interrogation or hardcoded question strings live here.
/// </summary>
public sealed class CaptureLeadState : IEngageState
{
    public string StateName => "CaptureLead";

    private readonly ResponseShaper _shaper;

    public CaptureLeadState(ResponseShaper shaper)
    {
        _shaper = shaper;
    }

    public Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var decision = ctx.TurnDecision;

        // Persist all slots the AI extracted this turn
        EngageSlotApplicator.Apply(ctx.Session, decision);

        ctx.Session.PendingCaptureMode = decision.ConversationComplete ? null : "Commercial";
        ctx.Session.ConversationState = "CaptureLead";
        ctx.Session.IsConversationComplete = decision.ConversationComplete;

        if (decision.ConversationComplete)
            ctx.Session.LastCompletedAtUtc = DateTime.UtcNow;

        // The AI reply is the response — no fallback copy.
        var rawReply = !string.IsNullOrWhiteSpace(decision.Reply)
            ? decision.Reply
            : "Thanks — let me make sure I have the right details to pass along.";

        var reply = _shaper.Shape(rawReply, ctx);

        return Task.FromResult(OperationResult<ChatSendResult>.Success(
            new ChatSendResult(ctx.Session.Id, reply, decision.Confidence, false, Array.Empty<EngageCitationResult>(), "CaptureLead")));
    }
}
