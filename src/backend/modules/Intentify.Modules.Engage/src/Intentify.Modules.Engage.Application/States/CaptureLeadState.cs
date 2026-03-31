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

        if (_policy.ShouldReopenCompletedConversation(ctx.Session, ctx.UserMessage))
        {
            _policy.ReopenConversation(ctx.Session, "reopen");
        }

        if (_policy.IsContextRecoverySignal(ctx.UserMessage))
        {
            var recovered = _shaper.Shape(_policy.BuildContextRecoveryPrompt(ctx.Session), ctx);
            ctx.Session.PendingCaptureMode = "Commercial";
            ctx.Session.ConversationState = "CaptureLead";
            ctx.Session.IsConversationComplete = false;

            return Task.FromResult(OperationResult<ChatSendResult>.Success(new ChatSendResult(
                ctx.Session.Id,
                recovered,
                0.85m,
                false,
                Array.Empty<EngageCitationResult>(),
                "CaptureLead")));
        }

        var missing = _policy.DeterminePrimaryMissingField(ctx.Session);
        string response;
        if (string.Equals(missing, "none", StringComparison.Ordinal))
        {
            _policy.MarkConversationCompleted(ctx.Session, "capture_complete");
            ctx.Session.PendingCaptureMode = null;
            response = ResolveDraftReply(ctx) ?? "Perfect — I’ve got the key details. Would you like me to have a teammate follow up?";
        }
        else
        {
            ctx.Session.PendingCaptureMode = "Commercial";
            ctx.Session.ConversationState = "CaptureLead";
            ctx.Session.IsConversationComplete = false;
            response = ResolveDraftReply(ctx) ?? _policy.BuildNaturalNextQuestion(ctx.Session, ctx);
        }

        var shaped = _shaper.Shape(response, ctx);

        return Task.FromResult(OperationResult<ChatSendResult>.Success(new ChatSendResult(
            ctx.Session.Id,
            shaped,
            0.82m,
            false,
            Array.Empty<EngageCitationResult>(),
            "CaptureLead")));
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
