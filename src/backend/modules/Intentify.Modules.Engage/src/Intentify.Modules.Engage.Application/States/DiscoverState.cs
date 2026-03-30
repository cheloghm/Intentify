using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application.States;

public sealed class DiscoverState : IEngageState
{
    public string StateName => "Discover";

    private readonly EngageConversationPolicy _policy;
    private readonly ResponseShaper _shaper;

    public DiscoverState(EngageConversationPolicy policy, ResponseShaper shaper)
    {
        _policy = policy;
        _shaper = shaper;
    }

    public async Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        // Merge short replies into slots (fixes "Grey", "the website", etc.)
        _policy.TryMergeShortReplySlots(ctx.Session, ctx.UserMessage, ctx.LastAssistantQuestion);

        var nextQuestion = _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        var response = _shaper.Shape(nextQuestion, ctx);

        ctx.Session.ConversationState = "Discover";

        // Helper method - you'll need to implement CreateAssistantResponse in Orchestrator or a shared helper
        return CreateAssistantResponse(ctx.Session, response, 0.65m, "Discover", "ContextAware");
    }

    // Temporary helper - move this to EngageOrchestrator later if needed
    private OperationResult<ChatSendResult> CreateAssistantResponse(EngageChatSession session, string response, decimal confidence, string path, string reason)
    {
        // Simple implementation - expand as needed
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(
            session.Id, response, confidence, false, Array.Empty<EngageCitationResult>(), path));
    }
}
