namespace Intentify.Modules.Engage.Application;

public enum RecommendationExecutionStatus
{
    Executed = 0,
    DisplayOnly = 1,
    NoOp = 2,
    Rejected = 3
}

public sealed record ExecuteRecommendationCommand(
    Guid TenantId,
    Guid SiteId,
    AiDecisionContextRef ContextRef,
    AiRecommendation Recommendation,
    bool Approved,
    IReadOnlyCollection<AiRecommendationType>? AllowlistedActions = null);

public sealed record RecommendationExecutionResult(
    RecommendationExecutionStatus Status,
    string Outcome,
    string? Reason = null,
    Guid? TicketId = null,
    string? DisplayLabel = null);
