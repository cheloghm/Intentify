using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

/// <summary>
/// Handles the first turn of a session.
/// The AI generates a warm, contextual opening based on the business briefing —
/// no hardcoded greeting copy lives here.
/// </summary>
public sealed class GreetingState : IEngageState
{
    public string StateName => "Greeting";

    private readonly ResponseShaper _shaper;

    public GreetingState(ResponseShaper shaper) => _shaper = shaper;

    public Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var decision = ctx.TurnDecision;

        // Apply any slots the AI may have extracted on the first turn (e.g. name from the opening message)
        EngageSlotApplicator.Apply(ctx.Session, decision);

        // Transition to Discover — the session progresses after the opening exchange
        ctx.Session.ConversationState = "Discover";
        ctx.Session.IsConversationComplete = false;
        ctx.Session.LastAssistantAskType = "none";

        // The AI reply is the greeting — no fallback copy.
        // If the AI service failed entirely (no bundle), provide a minimal honest opener.
        var rawReply = !string.IsNullOrWhiteSpace(decision.Reply)
            ? decision.Reply
            : "Hello — I'm here to help. What brings you here today?";

        var reply = _shaper.Shape(rawReply, ctx);

        return Task.FromResult(OperationResult<ChatSendResult>.Success(
            new ChatSendResult(ctx.Session.Id, reply, decision.Confidence, false, Array.Empty<EngageCitationResult>(), "Greeting")));
    }
}
