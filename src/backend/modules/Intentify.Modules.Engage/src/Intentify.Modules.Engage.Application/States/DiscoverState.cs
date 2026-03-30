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
        _policy.TryApplyStageContinuation(ctx.Session, ctx.UserMessage, ctx.LastAssistantQuestion);

        var action = ctx.PrimaryActionDecision?.Action ?? EngageNextAction.AskDiscoveryQuestion;
        var reason = ctx.PrimaryActionDecision?.Reason ?? string.Empty;

        if (action == EngageNextAction.EscalateSupport)
        {
            ctx.Session.PendingCaptureMode = "Support";
            ctx.Session.ConversationState = "Discover";
            var escalation = _shaper.Shape(
                "I can get a human teammate to help with this. What is the main issue and how should we contact you?",
                ctx);
            return CreateAssistantResponse(ctx.Session, escalation, 0.9m, "SupportEscalation", reason);
        }

        if (_policy.IsContextRecoverySignal(ctx.UserMessage))
        {
            var recovered = _shaper.Shape(_policy.BuildContextRecoveryPrompt(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";
            return CreateAssistantResponse(ctx.Session, recovered, 0.82m, "ContextRecovery", "AlreadyProvidedContext");
        }

        if (action == EngageNextAction.HandleNarrowObjection)
        {
            var objectionResponse = _shaper.Shape(_policy.BuildNarrowObjectionFollowUp(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";
            return CreateAssistantResponse(ctx.Session, objectionResponse, 0.8m, "ObjectionHandling", reason);
        }

        if (action == EngageNextAction.AnswerFactual && !string.IsNullOrWhiteSpace(ctx.KnowledgeSummary))
        {
            var topAnswer = ctx.KnowledgeSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? "Here’s what I found.";
            var factual = _shaper.Shape($"{topAnswer} { _policy.BuildNaturalNextQuestion(ctx.Session, ctx) }", ctx);
            ctx.Session.ConversationState = "Discover";
            return CreateAssistantResponse(ctx.Session, factual, 0.84m, "FactualAnswer", "KnowledgeBacked");
        }

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
