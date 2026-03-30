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

    public Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        _policy.TryApplyStageContinuation(ctx.Session, ctx.UserMessage, ctx.LastAssistantQuestion);

        if (_policy.IsContextRecoverySignal(ctx.UserMessage))
        {
            var recovered = _shaper.Shape(_policy.BuildContextRecoveryPrompt(ctx.Session), ctx);
            ctx.Session.PendingCaptureMode = "Commercial";
            ctx.Session.ConversationState = "CaptureLead";
            return Task.FromResult(OperationResult<ChatSendResult>.Success(new ChatSendResult(ctx.Session.Id, recovered, 0.85m, false, Array.Empty<EngageCitationResult>(), "CaptureLead")));
        }

        var response = _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        var shaped = _shaper.Shape(response, ctx);
        ctx.Session.PendingCaptureMode = "Commercial";
        ctx.Session.ConversationState = "CaptureLead";
        return Task.FromResult(OperationResult<ChatSendResult>.Success(new ChatSendResult(ctx.Session.Id, shaped, 0.8m, false, Array.Empty<EngageCitationResult>(), "CaptureLead")));
    }
}
