namespace Intentify.Modules.Engage.Api;

public sealed record WidgetBootstrapResponse(string SiteId, string Domain, string DisplayName, string BotName, string? PrimaryColor = null, bool? LauncherVisible = null);

public sealed record EngageChatSendRequest(string WidgetKey, string? SessionId, string Message, string? CollectorSessionId = null);

public sealed record EngageCitationResponse(string SourceId, string ChunkId, int ChunkIndex);

public sealed record EngageAiDecisionContextRefResponse(string TenantId, string SiteId, string? VisitorId = null, string? EngageSessionId = null);

public sealed record EngageAiEvidenceRefResponse(string Source, string ReferenceId, string? Detail = null);

public sealed record EngageAiTargetRefsResponse(
    string? PromoId = null,
    string? PromoPublicKey = null,
    string? KnowledgeSourceId = null,
    string? TicketId = null,
    string? VisitorId = null);

public sealed record EngageAiRecommendationResponse(
    string Type,
    decimal Confidence,
    string Rationale,
    IReadOnlyCollection<EngageAiEvidenceRefResponse>? EvidenceRefs,
    EngageAiTargetRefsResponse? TargetRefs,
    bool RequiresApproval,
    IReadOnlyDictionary<string, string>? ProposedCommand);

public sealed record EngageAiDecisionResponse(
    string SchemaVersion,
    string DecisionId,
    EngageAiDecisionContextRefResponse? ContextRef,
    decimal OverallConfidence,
    IReadOnlyCollection<EngageAiRecommendationResponse>? Recommendations,
    string ValidationStatus,
    IReadOnlyCollection<string>? ValidationErrors,
    IReadOnlyCollection<string>? AllowlistedActions,
    bool ShouldFallback,
    string? FallbackReason,
    string? NoActionMessage);

public sealed record EngageChatSendResponse(
    string SessionId,
    string Response,
    decimal Confidence,
    bool TicketCreated,
    IReadOnlyCollection<EngageCitationResponse> Sources,
    string? ResponseKind = null,
    string? PromoPublicKey = null,
    string? PromoTitle = null,
    string? PromoDescription = null,
    EngageAiDecisionResponse? Stage7Decision = null,
    string? OpportunityLabel = null,
    int? IntentScore = null,
    string? ConversationSummary = null,
    string? SuggestedFollowUp = null);

public sealed record ConversationSummaryResponse(string SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ConversationMessageResponse(
    string MessageId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    decimal? Confidence,
    IReadOnlyCollection<EngageCitationResponse> Citations);


public sealed record EngageBotResponse(string SiteId, string Name, string? PrimaryColor = null, bool? LauncherVisible = null, string? Tone = null, string? Verbosity = null, string? FallbackStyle = null);

public sealed record UpdateEngageBotRequest(string Name, string? PrimaryColor = null, bool? LauncherVisible = null, string? Tone = null, string? Verbosity = null, string? FallbackStyle = null);
