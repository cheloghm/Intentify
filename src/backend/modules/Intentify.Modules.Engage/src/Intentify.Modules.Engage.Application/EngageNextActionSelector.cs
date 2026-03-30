using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageNextActionSelector
{
    private readonly EngageConversationPolicy _policy;

    public EngageNextActionSelector(EngageConversationPolicy policy)
    {
        _policy = policy;
    }

    public EngageNextActionDecision Select(EngageConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Analysis.IsInitialTurn)
        {
            return new EngageNextActionDecision(EngageNextAction.Greeting, "Greeting", "InitialTurn");
        }

        if (ShouldCapture(context))
        {
            return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "CaptureLead", "CaptureSignal");
        }

        return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "DefaultDiscovery");
    }

    private bool ShouldCapture(EngageConversationContext context)
    {
        if (string.Equals(context.Session.ConversationState, "CaptureLead", StringComparison.Ordinal))
        {
            return true;
        }

        if (context.Analysis.AiSuggestedCapture)
        {
            return true;
        }

        var explicitContactRequest = _policy.IsExplicitCommercialContactRequest(context.UserMessage);
        return _policy.IsCommercialCaptureReady(context.Session, explicitContactRequest);
    }
}
