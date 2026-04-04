namespace Intentify.Modules.Engage.Api;

public sealed record WidgetBootstrapResponse(string SiteId, string Domain, string DisplayName, string BotName, string? PrimaryColor = null, bool? LauncherVisible = null);

public sealed record EngageChatSendRequest(string WidgetKey, string? SessionId, string Message, string? CollectorSessionId = null, string? VisitorId = null);

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
    string? SecondaryResponse = null,
    EngageAiDecisionResponse? Stage7Decision = null,
    string? OpportunityLabel = null,
    int? IntentScore = null,
    string? ConversationSummary = null,
    string? SuggestedFollowUp = null,
    string? PreferredContactMethod = null,
    string? FollowUpEmailDraft = null,
    string? NextBestAction = null);

public sealed record ConversationSummaryResponse(string SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record OpportunityContactMethodBreakdownResponse(int Email, int Phone, int Unknown);

public sealed record OpportunityDailyPointResponse(DateTime DateUtc, int Count);

public sealed record OpportunityAnalyticsResponse(
    int TotalCommercialOpportunities,
    int CommercialCount,
    int SupportCount,
    int GeneralCount,
    int HighIntentCount,
    OpportunityContactMethodBreakdownResponse PreferredContactMethodDistribution,
    IReadOnlyCollection<OpportunityDailyPointResponse> OpportunitiesOverTime);

public sealed record ConversationMessageResponse(
    string MessageId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    decimal? Confidence,
    IReadOnlyCollection<EngageCitationResponse> Citations);


// TODO(tracking): The first field name is SiteId but endpoints currently populate it with BotId (N format).
// Keep as-is for compatibility in this phase; align DTO naming/serialization in a dedicated cleanup.
public sealed record EngageBotResponse(
    string SiteId,
    string Name,
    string? PrimaryColor = null,
    bool? LauncherVisible = null,
    string? Tone = null,
    string? Verbosity = null,
    string? FallbackStyle = null,
    string? BusinessDescription = null,
    string? Industry = null,
    string? ServicesDescription = null,
    string? GeoFocus = null,
    string? PersonalityDescriptor = null,
    bool DigestEmailEnabled = false,
    string? DigestEmailRecipients = null,
    string? DigestEmailFrequency = null);

public sealed record UpdateEngageBotRequest(
    string Name,
    string? PrimaryColor = null,
    bool? LauncherVisible = null,
    string? Tone = null,
    string? Verbosity = null,
    string? FallbackStyle = null,
    string? BusinessDescription = null,
    string? Industry = null,
    string? ServicesDescription = null,
    string? GeoFocus = null,
    string? PersonalityDescriptor = null,
    bool DigestEmailEnabled = false,
    string? DigestEmailRecipients = null,
    string? DigestEmailFrequency = null);

public sealed record DigestSendRequest(string SiteId);
