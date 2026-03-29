using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

public sealed class DiscoverState : IEngageState
{
    public string StateName => "Discover";
    private readonly EngageConversationPolicy _policy;
    private readonly ResponseShaper _shaper;

    public async Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var slotsFilled = _policy.TryMergeShortReplySlots(ctx.Session, ctx.UserMessage, ctx.LastQuestion);
        if (_policy.IsCommercialCaptureReady(ctx.Session, false))
        {
            return await CaptureLeadState.TransitionToCapture(ctx, ct);
        }

        var nextQuestion = _policy.BuildNextDiscoveryQuestion(ctx.Session);
        var response = _shaper.Shape(nextQuestion, ctx);
        ctx.Session.ConversationState = "Discover";
        return CreateAssistantResponse(ctx.Session, response, 0.65m, "Discover", "ContextAware");
    }
}
