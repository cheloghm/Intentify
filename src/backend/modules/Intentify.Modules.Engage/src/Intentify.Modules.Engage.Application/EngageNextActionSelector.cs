namespace Intentify.Modules.Engage.Application;

/// <summary>
/// Maps the AI's structured turn decision to an EngageNextActionDecision for routing.
/// All routing logic is derived from the AI output — no hardcoded signal evaluation.
/// </summary>
public sealed class EngageNextActionSelector
{
    public EngageNextActionDecision Select(EngageConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // First turn in the session — route to greeting state so the AI opens the conversation
        if (context.Analysis.IsInitialTurn)
            return new EngageNextActionDecision(EngageNextAction.Greeting, "Greeting", "InitialTurn");

        var decision = context.TurnDecision;

        // AI explicitly requested ticket / human escalation
        if (decision.CreateTicket)
            return new EngageNextActionDecision(EngageNextAction.EscalateSupport, "Discover", "AiCreateTicket");

        // AI determined lead is ready to create
        if (decision.CreateLead)
            return new EngageNextActionDecision(EngageNextAction.AskCaptureQuestion, "Discover", "AiCreateLead");

        // AI determined the conversation has reached a natural close
        if (decision.ConversationComplete)
            return new EngageNextActionDecision(EngageNextAction.CloseConversation, "Discover", "AiConversationComplete");

        // Default — AI drives reply content; Discover state applies it
        return new EngageNextActionDecision(EngageNextAction.AskDiscoveryQuestion, "Discover", "AiDriven");
    }
}
