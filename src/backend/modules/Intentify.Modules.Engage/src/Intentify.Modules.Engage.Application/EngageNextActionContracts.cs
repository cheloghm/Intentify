namespace Intentify.Modules.Engage.Application;

public enum EngageNextAction
{
    Greeting = 0,
    AskDiscoveryQuestion = 1,
    AskCaptureQuestion = 2,
    EscalateSupport = 3,
    AnswerFactual = 4
    AskCaptureQuestion = 2
}

public sealed record EngageNextActionDecision(
    EngageNextAction Action,
    string TargetState,
    string Reason);
