using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

public sealed class CaptureLeadState : IEngageState
{
    public string StateName => "CaptureLead";
    private readonly EngageConversationPolicy _policy;
    private readonly ResponseShaper _shaper;

    public CaptureLeadState(EngageConversationPolicy policy, ResponseShaper shaper)
    {
        _policy = policy;
        _shaper = shaper;
    }

    public async Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var response = _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        var shaped = _shaper.Shape(response, ctx);
        ctx.Session.ConversationState = "CaptureLead";
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(ctx.Session.Id, shaped, 0.8m, false, Array.Empty<EngageCitationResult>(), "CaptureLead"));
    }

    // Helper for transition from Discover
    public static async Task<OperationResult<ChatSendResult>> TransitionToCapture(EngageConversationContext ctx, CancellationToken ct)
    {
        var state = new CaptureLeadState(null!, null!); // inject properly in real code
        return await state.HandleAsync(ctx, ct);
    }
}
