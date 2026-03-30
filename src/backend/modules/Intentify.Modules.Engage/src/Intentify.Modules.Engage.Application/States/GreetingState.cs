using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

public sealed class GreetingState : IEngageState
{
    public string StateName => "Greeting";
    private readonly ResponseShaper _shaper;

    public GreetingState(ResponseShaper shaper) => _shaper = shaper;

    public async Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        var response = _shaper.Shape("Hi! How can I help you today?", ctx);
        ctx.Session.ConversationState = "Greeting";
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(ctx.Session.Id, response, 1.0m, false, Array.Empty<EngageCitationResult>(), "Greeting"));
    }
}
