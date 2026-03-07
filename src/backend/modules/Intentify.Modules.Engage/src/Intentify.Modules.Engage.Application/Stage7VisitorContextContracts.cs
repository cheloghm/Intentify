using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Tickets.Application;

namespace Intentify.Modules.Engage.Application;

public sealed record BuildVisitorContextBundleQuery(
    Guid TenantId,
    Guid SiteId,
    Guid? VisitorId,
    Guid? EngageSessionId,
    string KnowledgeQuery,
    int KnowledgeTop = 3,
    int TimelineLimit = 20,
    int EngageMessageLimit = 12,
    int TicketsLimit = 10,
    int PromoEntriesLimit = 10,
    bool IncludeIntelligenceSnapshot = false,
    string IntelligenceCategory = "general",
    string IntelligenceLocation = "US",
    string IntelligenceTimeWindow = "7d",
    string? IntelligenceProvider = null,
    string? IntelligenceKeyword = null,
    string? IntelligenceAudienceType = null,
    int? IntelligenceLimit = 5);

public sealed record Stage7VisitorContextBundle(
    AiDecisionContextRef ContextRef,
    IReadOnlyCollection<string> CollectorSessionIds,
    Stage7KnowledgeRetrievalSnapshot KnowledgeRetrievalSnapshot,
    Stage7VisitorProfileSummary? VisitorProfile,
    IReadOnlyCollection<Stage7TimelineItemSummary>? RecentTimelineSummary,
    Stage7EngageSessionSummary? RecentEngageSummary,
    IReadOnlyCollection<Stage7TicketSummary>? LinkedTicketsSummary,
    IReadOnlyCollection<Stage7PromoInteractionSummary>? PromoInteractionSummary,
    Stage7IntelligenceSnapshot? IntelligenceSnapshot);

public sealed record Stage7VisitorProfileSummary(
    Guid VisitorId,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    int VisitCount,
    int TotalPagesVisited,
    string? PrimaryEmail,
    string? DisplayName,
    string? Phone,
    string? Language,
    string? Platform);

public sealed record Stage7TimelineItemSummary(
    DateTime OccurredAtUtc,
    string Type,
    string Url,
    string? SessionId,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record Stage7EngageMessageSummary(
    string Role,
    string ContentExcerpt,
    DateTime CreatedAtUtc,
    decimal? Confidence,
    int CitationCount);

public sealed record Stage7EngageSessionSummary(
    Guid SessionId,
    DateTime StartedAtUtc,
    DateTime LastActivityAtUtc,
    IReadOnlyCollection<Stage7EngageMessageSummary> Messages);

public sealed record Stage7TicketSummary(
    Guid TicketId,
    string Subject,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? EngageSessionId,
    Guid? VisitorId);

public sealed record Stage7PromoInteractionSummary(
    Guid PromoEntryId,
    Guid PromoId,
    DateTime SubmittedAtUtc,
    string? Email,
    string? Name,
    IReadOnlyDictionary<string, string>? AnswerHighlights);

public sealed record Stage7RetrievedKnowledgeChunkSummary(
    Guid SourceId,
    Guid ChunkId,
    int ChunkIndex,
    int Score,
    string ContentExcerpt);

public sealed record Stage7KnowledgeRetrievalSnapshot(
    string Query,
    int TopK,
    IReadOnlyCollection<Stage7RetrievedKnowledgeChunkSummary> TopChunks);

public sealed record Stage7IntelligenceSnapshot(
    string Category,
    string Location,
    string TimeWindow,
    string? Provider,
    DateTime? RefreshedAtUtc,
    int TotalItems,
    IntelligenceDashboardSummaryResponse Summary,
    IReadOnlyCollection<IntelligenceDashboardTrendItemResponse> TopItems);
