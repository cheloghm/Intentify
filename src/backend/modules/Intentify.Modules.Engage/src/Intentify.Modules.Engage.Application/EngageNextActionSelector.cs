using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageNextActionSelector
{
    private readonly EngageConversationPolicy _policy;
    private readonly EngageInputInterpreter _inputInterpreter = new();

    public EngageNextActionSelector(EngageConversationPolicy policy)
    {
        _policy = policy;
    }

    public EngageNextActionDecision Select(EngageConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var memory = EngageSessionMemorySnapshot.FromContext(context, _policy);
        var normalized = _inputInterpreter.NormalizeUserMessage(context.UserMessage);

        if (_policy.ShouldReopenCompletedConversation(context.Session, context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "ReopenConversation");
        }

        if (_policy.IsConversationCloseSignal(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.CloseConversation, "Discover", "ConversationClose");
        }

        if (_policy.IsExplicitEscalationRequest(context.UserMessage) || _policy.NeedsHumanHelp(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "SupportEscalation");
        }

        if (TryResolveAiAuthoredAction(context.AiDecision, out var aiDecision))
        {
            return aiDecision;
        }

        if (ShouldAnswerFactual(context))
        {
            return new EngageNextActionDecision(EngageNextAction.AnswerFactual, "Discover", "KnowledgeFirst");
        }

        if (memory.IsSupportCaptureActive)
        {
            if (_policy.IsSupportCaptureComplete(context.Session))
            {
                return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "SupportCaptureComplete");
            }

            return new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "ActiveSupportCapture");
        }

        if (_policy.IsNarrowObjectionSignal(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.HandleNarrowObjection, "Discover", "NarrowObjection");
        }

        var captureSignal = _policy.IsStrongCommercialIntent(context.UserMessage)
                            || _policy.IsExplicitCommercialContactRequest(context.UserMessage)
                            || memory.IsCommercialCaptureActive
                            || (context.Analysis.AnswersPreviousQuestion && memory.DiscoveryFieldCount > 0);

        if (captureSignal)
        {
            return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "CaptureProgression");
        }

        if (context.Analysis.IsInitialTurn && (normalized is "hi" or "hello" or "hey" || _inputInterpreter.IsLikelyGreetingTypo(normalized)))
        {
            return new EngageNextActionDecision(EngageNextAction.Greeting, "Greeting", "InitialGreeting");
        }

        return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "DefaultDiscovery");
    }

    private bool ShouldAnswerFactual(EngageConversationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.KnowledgeSummary))
        {
            return false;
        }

        var message = context.UserMessage.Trim();
        var normalized = _inputInterpreter.NormalizeUserMessage(context.UserMessage);

        return message.Contains('?', StringComparison.Ordinal)
               || message.StartsWith("what ", StringComparison.OrdinalIgnoreCase)
               || message.StartsWith("how ", StringComparison.OrdinalIgnoreCase)
               || message.StartsWith("when ", StringComparison.OrdinalIgnoreCase)
               || message.StartsWith("where ", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("your services", StringComparison.Ordinal)
               || normalized.Contains("services", StringComparison.Ordinal)
               || normalized.Contains("pricing", StringComparison.Ordinal)
               || normalized.Contains("cost", StringComparison.Ordinal)
               || normalized.Contains("offer", StringComparison.Ordinal);
    }

    private static bool TryResolveAiAuthoredAction(AiDecisionContract decision, out EngageNextActionDecision resolved)
    {
        resolved = default!;

        var command = decision.Recommendations?
            .Select(item => item.ProposedCommand)
            .FirstOrDefault(item => item is { Count: > 0 });

        if (command is null)
        {
            return false;
        }

        if (!command.TryGetValue("assistantMove", out var move) || string.IsNullOrWhiteSpace(move))
        {
            return false;
        }

        move = move.Trim();
        resolved = move.ToLowerInvariant() switch
        {
            "close" => new EngageNextActionDecision(EngageNextAction.CloseConversation, "Discover", "AiPlannerClose"),
            "escalate" => new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "AiPlannerEscalate"),
            "factual" => new EngageNextActionDecision(EngageNextAction.AnswerFactual, "Discover", "AiPlannerFactual"),
            "capture" => new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "AiPlannerCapture"),
            "greet" => new EngageNextActionDecision(EngageNextAction.Greeting, "Greeting", "AiPlannerGreeting"),
            _ => new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "AiPlannerDiscovery")
        };

        return true;
    }
}
