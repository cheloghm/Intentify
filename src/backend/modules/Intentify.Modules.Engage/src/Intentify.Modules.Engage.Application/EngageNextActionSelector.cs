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

        if (_policy.IsConversationCloseSignal(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.CloseConversation, "Discover", "ConversationClose");
        }

        if (_policy.IsExplicitEscalationRequest(context.UserMessage) || _policy.NeedsHumanHelp(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "SupportEscalation");
        }

        if (memory.IsSupportCaptureActive)
        {
            if (_policy.IsSupportCaptureComplete(context.Session))
            {
                return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "SupportCaptureComplete");
            }

            if (ShouldAnswerFactual(context))
            {
                return new EngageNextActionDecision(EngageNextAction.AnswerFactual, "Discover", "SupportPivotFactual");
            }

            if (_policy.IsStrongCommercialIntent(context.UserMessage) || _policy.IsExplicitCommercialContactRequest(context.UserMessage))
            {
                return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "SupportPivotCommercial");
            }

            return new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "ActiveSupportCapture");
        }

        if (context.Analysis.AnswersPreviousQuestion && memory.IsCommercialCaptureActive)
        {
            return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "CaptureContinuation");
        }

        if (context.Analysis.AnswersPreviousQuestion
            && string.Equals(memory.ActiveStage, "Discover", StringComparison.Ordinal))
        {
            return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "DiscoveryContinuation");
        }

        if (context.Analysis.IsInitialTurn)
        {
            return new EngageNextActionDecision(EngageNextAction.Greeting, "Greeting", "InitialTurn");
        }

        if (_policy.IsNarrowObjectionSignal(context.UserMessage))
        {
            return new EngageNextActionDecision(EngageNextAction.HandleNarrowObjection, "Discover", "NarrowObjection");
        }

        if (memory.IsCommercialCaptureActive || ShouldCapture(context, memory))
        {
            return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "CaptureSignal");
        }

        if (ShouldAnswerFactual(context))
        {
            return new EngageNextActionDecision(EngageNextAction.AnswerFactual, "Discover", "FactualKnowledgeAnswer");
        }

        return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "DefaultDiscovery");
    }

    private bool ShouldCapture(EngageConversationContext context, EngageSessionMemorySnapshot memory)
    {
        if (context.Analysis.AiSuggestedCapture)
        if (string.Equals(context.Session.ConversationState, "CaptureLead", StringComparison.Ordinal))
        {
            return true;
        }

        if (memory.LeadReady)
        {
            return true;
        }

        return _policy.IsStrongCommercialIntent(context.UserMessage) && memory.DiscoveryFieldCount >= 2;
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
            || normalized.Contains("cost", StringComparison.Ordinal);
    }
}
