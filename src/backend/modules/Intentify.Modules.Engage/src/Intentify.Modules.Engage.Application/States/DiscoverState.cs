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

        if (action == EngageNextAction.EscalateSupport)
        {
            ctx.Session.PendingCaptureMode = "Support";
            ctx.Session.ConversationState = "Discover";

            var escalation = _shaper.Shape(_policy.BuildSupportCapturePrompt(ctx.Session), ctx);

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, escalation, 0.9m, "SupportEscalation", reason));
        }

        if (action == EngageNextAction.CloseConversation)
        {
            ctx.Session.PendingCaptureMode = null;
            ctx.Session.ConversationState = "Discover";

            var close = _shaper.Shape(
                "You’re all set — happy to help. If anything comes up, just message me.",
                ctx);

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, close, 0.88m, "ConversationClose", reason));
        }

        if (_policy.IsContextRecoverySignal(ctx.UserMessage))
        {
            var recovered = _shaper.Shape(_policy.BuildContextRecoveryPrompt(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, recovered, 0.82m, "ContextRecovery", "AlreadyProvidedContext"));
        }

        if (string.Equals(reason, "SupportCaptureComplete", StringComparison.Ordinal))
        {
            ctx.Session.PendingCaptureMode = null;
            ctx.Session.ConversationState = "Discover";
            var done = _shaper.Shape(
                "Thanks — I’ve captured what our support team needs. Is there anything else you’d like help with?",
                ctx);

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, done, 0.86m, "SupportEscalation", reason));
        }

        if (action == EngageNextAction.HandleNarrowObjection)
        {
            var objectionResponse = _shaper.Shape(_policy.BuildNarrowObjectionFollowUp(ctx.Session), ctx);
            ctx.Session.ConversationState = "Discover";

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, objectionResponse, 0.8m, "ObjectionHandling", reason));
        }

        if (action == EngageNextAction.AnswerFactual && !string.IsNullOrWhiteSpace(ctx.KnowledgeSummary))
        {
            if (string.Equals(ctx.Session.PendingCaptureMode, "Support", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Session.PendingCaptureMode = null;
            }

            var topAnswer = ComposeFactualAnswer(ctx.KnowledgeSummary, ctx.UserMessage);

            var factual = _shaper.Shape(topAnswer, ctx);

            ctx.Session.ConversationState = "Discover";

            return Task.FromResult(
                CreateAssistantResponse(ctx.Session, factual, 0.84m, "FactualAnswer", "KnowledgeBacked"));
        }

        var nextQuestion = _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        var response = _shaper.Shape(nextQuestion, ctx);

        ctx.Session.ConversationState = "Discover";

        return Task.FromResult(
            CreateAssistantResponse(ctx.Session, response, 0.65m, "Discover", "ContextAware"));
    }

    private OperationResult<ChatSendResult> CreateAssistantResponse(
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

    private static string ComposeFactualAnswer(string knowledgeSummary, string userMessage)
    {
        var lines = knowledgeSummary
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        if (lines.Length == 0)
        {
            return "Here’s what I found.";
        }

        var primary = lines.FirstOrDefault(item =>
                item.Length >= 35
                || item.Contains('.', StringComparison.Ordinal)
                || item.Contains(',', StringComparison.Ordinal))
            ?? lines[0];

        var normalized = userMessage.Trim().ToLowerInvariant();
        var serviceIntent = normalized.Contains("service", StringComparison.Ordinal)
            || normalized.Contains("offer", StringComparison.Ordinal)
            || normalized.Contains("pricing", StringComparison.Ordinal)
            || normalized.Contains("cost", StringComparison.Ordinal);

        if (serviceIntent && primary.Length < 55 && lines.Length > 1)
        {
            var next = lines.FirstOrDefault(item => !string.Equals(item, primary, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(next))
            {
                return $"{primary} {next}";
            }
        }

        return primary;
    }
}
