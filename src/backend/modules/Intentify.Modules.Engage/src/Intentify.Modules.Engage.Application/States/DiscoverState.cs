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

    public Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext ctx, CancellationToken ct)
    {
        _policy.TryApplyStageContinuation(ctx.Session, ctx.UserMessage, ctx.LastAssistantQuestion);

        var action = ctx.PrimaryActionDecision?.Action ?? EngageNextAction.AskDiscoveryQuestion;
        var reason = ctx.PrimaryActionDecision?.Reason ?? string.Empty;

        if (string.Equals(reason, "ReopenConversation", StringComparison.Ordinal))
        {
            _policy.ReopenConversation(ctx.Session, "reopen");
            var reopened = _shaper.Shape("Absolutely — happy to help with a new question. What would you like to explore?", ctx);
            return Task.FromResult(CreateAssistantResponse(ctx.Session, reopened, 0.9m, "Reopen", reason));
        }

        if (action == EngageNextAction.EscalateSupport)
        {
            ctx.Session.PendingCaptureMode = "Support";
            ctx.Session.ConversationState = "Discover";
            ctx.Session.IsConversationComplete = false;

            var escalation = ResolveDraftReply(ctx) ?? _policy.BuildSupportCapturePrompt(ctx.Session);
            escalation = _shaper.Shape(escalation, ctx);

            return Task.FromResult(CreateAssistantResponse(ctx.Session, escalation, 0.9m, "SupportEscalation", reason));
        }

        if (action == EngageNextAction.CloseConversation)
        {
            _policy.MarkConversationCompleted(ctx.Session, "closed");
            var close = ResolveDraftReply(ctx) ?? "You’re all set — happy to help. If anything new comes up, just message me.";
            close = _shaper.Shape(close, ctx);

            return Task.FromResult(CreateAssistantResponse(ctx.Session, close, 0.9m, "ConversationClose", reason));
        }

        if (_policy.IsContextRecoverySignal(ctx.UserMessage))
        {
            var recovered = _shaper.Shape(_policy.BuildContextRecoveryPrompt(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";
            ctx.Session.IsConversationComplete = false;

            return Task.FromResult(CreateAssistantResponse(ctx.Session, recovered, 0.82m, "ContextRecovery", "AlreadyProvidedContext"));
        }

        if (string.Equals(reason, "SupportCaptureComplete", StringComparison.Ordinal))
        {
            ctx.Session.PendingCaptureMode = null;
            ctx.Session.ConversationState = "Discover";
            var done = _shaper.Shape("Thanks — I’ve captured what our support team needs. Anything else I can help with?", ctx);

            return Task.FromResult(CreateAssistantResponse(ctx.Session, done, 0.86m, "SupportEscalation", reason));
        }

        if (action == EngageNextAction.HandleNarrowObjection)
        {
            var objectionResponse = _shaper.Shape(_policy.BuildNarrowObjectionFollowUp(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";
            ctx.Session.IsConversationComplete = false;

            return Task.FromResult(CreateAssistantResponse(ctx.Session, objectionResponse, 0.8m, "ObjectionHandling", reason));
        }

        if (action == EngageNextAction.AnswerFactual)
        {
            if (string.Equals(ctx.Session.PendingCaptureMode, "Support", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Session.PendingCaptureMode = null;
            }

            var topAnswer = ResolveDraftReply(ctx) ?? _policy.BuildGroundedKnowledgeAnswer(ctx.KnowledgeSummary, ctx.UserMessage);
            var factual = _shaper.Shape(topAnswer, ctx);
            ctx.Session.ConversationState = "Discover";
            ctx.Session.IsConversationComplete = false;
            ctx.Session.LastAssistantAskType = "none";

            return Task.FromResult(CreateAssistantResponse(ctx.Session, factual, 0.86m, "FactualAnswer", "KnowledgeBacked"));
        }

        var nextQuestion = ResolveDraftReply(ctx) ?? _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        var response = _shaper.Shape(nextQuestion, ctx);

        ctx.Session.ConversationState = "Discover";
        var missing = _policy.DeterminePrimaryMissingField(ctx.Session);
        if (string.Equals(missing, "none", StringComparison.Ordinal))
        {
            _policy.MarkConversationCompleted(ctx.Session, "capture_complete");
        }
        else
        {
            ctx.Session.IsConversationComplete = false;
        }

        return Task.FromResult(CreateAssistantResponse(ctx.Session, response, 0.72m, "Discover", reason));
    }

    private static OperationResult<ChatSendResult> CreateAssistantResponse(
        EngageChatSession session,
        string response,
        decimal confidence,
        string path,
        string reason)
    {
        return OperationResult<ChatSendResult>.Success(
            new ChatSendResult(
                session.Id,
                response,
                confidence,
                false,
                Array.Empty<EngageCitationResult>(),
                path));
    }

    private static string? ResolveDraftReply(EngageConversationContext ctx)
    {
        var command = ctx.AiDecision.Recommendations?
            .Select(item => item.ProposedCommand)
            .FirstOrDefault(item => item is { Count: > 0 });

        if (command is null)
        {
            return null;
        }

        return command.TryGetValue("draftReply", out var draft) && !string.IsNullOrWhiteSpace(draft)
            ? draft.Trim()
            : null;
    }
}
