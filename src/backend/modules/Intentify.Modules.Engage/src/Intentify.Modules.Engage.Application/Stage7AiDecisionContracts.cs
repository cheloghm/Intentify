namespace Intentify.Modules.Engage.Application;

public enum AiDecisionValidationStatus
{
    Valid = 0,
    Invalid = 1
}

public enum AiRecommendationType
{
    SuggestPromo = 0,
    SuggestKnowledge = 1,
    EscalateTicket = 2,
    TagVisitor = 3,
    SuggestKnowledgeUpdate = 4,
    NotifyClientKnowledgeGap = 5,
    NoAction = 6
}

public sealed record AiDecisionContextRef(
    Guid TenantId,
    Guid SiteId,
    Guid? VisitorId = null,
    Guid? EngageSessionId = null);

public sealed record AiEvidenceRef(
    string Source,
    string ReferenceId,
    string? Detail = null);

public sealed record AiTargetRefs(
    Guid? PromoId = null,
    string? PromoPublicKey = null,
    Guid? KnowledgeSourceId = null,
    Guid? TicketId = null,
    Guid? VisitorId = null);

public sealed record AiRecommendation(
    AiRecommendationType Type,
    decimal Confidence,
    string Rationale,
    IReadOnlyCollection<AiEvidenceRef>? EvidenceRefs,
    AiTargetRefs? TargetRefs,
    bool RequiresApproval,
    IReadOnlyDictionary<string, string>? ProposedCommand);

public sealed record AiDecisionContract(
    string SchemaVersion,
    string DecisionId,
    AiDecisionContextRef? ContextRef,
    decimal OverallConfidence,
    IReadOnlyCollection<AiRecommendation>? Recommendations,
    AiDecisionValidationStatus ValidationStatus,
    IReadOnlyCollection<string>? ValidationErrors,
    IReadOnlyCollection<AiRecommendationType>? AllowlistedActions,
    bool ShouldFallback,
    string? FallbackReason,
    string? NoActionMessage);
