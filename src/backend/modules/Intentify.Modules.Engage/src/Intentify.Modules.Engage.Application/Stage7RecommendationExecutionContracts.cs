namespace Intentify.Modules.Engage.Application;

public enum Stage7RecommendationExecutionStatus
{
    Executed = 0,
    DisplayOnly = 1,
    NoOp = 2,
    Rejected = 3
}

public sealed record ExecuteStage7RecommendationCommand(
    Guid TenantId,
    Guid SiteId,
    AiDecisionContextRef ContextRef,
    AiRecommendation Recommendation,
    bool Approved,
    IReadOnlyCollection<AiRecommendationType>? AllowlistedActions = null);

public sealed record Stage7RecommendationExecutionResult(
    Stage7RecommendationExecutionStatus Status,
    string Outcome,
    string? Reason = null,
    Guid? TicketId = null,
    string? DisplayLabel = null);
