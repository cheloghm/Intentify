using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class VisitorContextBundleHandler
{
    private const int MaxKnowledgeTop = 5;
    private const int MaxTimelineLimit = 30;
    private const int MaxEngageMessageLimit = 20;
    private const int MaxTicketsLimit = 20;
    private const int MaxPromoEntriesLimit = 20;
    private const int ExcerptLength = 220;

    private readonly IEngageChatSessionRepository _chatSessionRepository;
    private readonly IEngageChatMessageRepository _chatMessageRepository;
    private readonly ILeadVisitorLinker _leadVisitorLinker;
    private readonly RetrieveTopChunksHandler _retrieveTopChunksHandler;
    private readonly IKnowledgeQuickFactsRepository _quickFactsRepository;
    private readonly GetVisitorDetailHandler _getVisitorDetailHandler;
    private readonly GetVisitorTimelineHandler _getVisitorTimelineHandler;
    private readonly ListTicketsHandler _listTicketsHandler;
    private readonly IPromoEntryRepository _promoEntryRepository;
    private readonly QueryIntelligenceTrendsService _queryIntelligenceTrendsService;

    public VisitorContextBundleHandler(
        IEngageChatSessionRepository chatSessionRepository,
        IEngageChatMessageRepository chatMessageRepository,
        ILeadVisitorLinker leadVisitorLinker,
        RetrieveTopChunksHandler retrieveTopChunksHandler,
        IKnowledgeQuickFactsRepository quickFactsRepository,
        GetVisitorDetailHandler getVisitorDetailHandler,
        GetVisitorTimelineHandler getVisitorTimelineHandler,
        ListTicketsHandler listTicketsHandler,
        IPromoEntryRepository promoEntryRepository,
        QueryIntelligenceTrendsService queryIntelligenceTrendsService)
    {
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _leadVisitorLinker = leadVisitorLinker;
        _retrieveTopChunksHandler = retrieveTopChunksHandler;
        _quickFactsRepository = quickFactsRepository;
        _getVisitorDetailHandler = getVisitorDetailHandler;
        _getVisitorTimelineHandler = getVisitorTimelineHandler;
        _listTicketsHandler = listTicketsHandler;
        _promoEntryRepository = promoEntryRepository;
        _queryIntelligenceTrendsService = queryIntelligenceTrendsService;
    }

    public async Task<OperationResult<VisitorContextBundle>> HandleAsync(
        BuildVisitorContextBundleQuery query,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(query);
        if (errors.HasErrors)
        {
            return OperationResult<VisitorContextBundle>.ValidationFailed(errors);
        }

        var engageSession = query.EngageSessionId.HasValue
            ? await _chatSessionRepository.GetByIdAsync(query.EngageSessionId.Value, cancellationToken)
            : null;

        if (query.EngageSessionId.HasValue)
        {
            if (engageSession is null || engageSession.TenantId != query.TenantId || engageSession.SiteId != query.SiteId)
            {
                return OperationResult<VisitorContextBundle>.NotFound();
            }
        }

        var resolvedVisitorId = query.VisitorId;
        var collectorSessionIds = new HashSet<string>(StringComparer.Ordinal);

        if (engageSession is not null && !string.IsNullOrWhiteSpace(engageSession.CollectorSessionId))
        {
            collectorSessionIds.Add(engageSession.CollectorSessionId.Trim());

            if (resolvedVisitorId is null)
            {
                resolvedVisitorId = await _leadVisitorLinker.ResolveVisitorIdAsync(
                    query.TenantId,
                    query.SiteId,
                    null,
                    null,
                    engageSession.CollectorSessionId,
                    cancellationToken);
            }
        }

        var knowledgeTop = Clamp(query.KnowledgeTop, 1, MaxKnowledgeTop);
        var retrievedChunks = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(
                query.TenantId,
                query.SiteId,
                query.KnowledgeQuery.Trim(),
                knowledgeTop,
                engageSession?.BotId),
            cancellationToken);

        var topChunks = retrievedChunks
            .Take(knowledgeTop)
            .Select(item => new RetrievedKnowledgeChunkSummary(
                item.SourceId,
                item.ChunkId,
                item.ChunkIndex,
                item.Score,
                ToExcerpt(item.Content, ExcerptLength)))
            .ToArray();

        var knowledgeSnapshot = new KnowledgeRetrievalSnapshot(
            query.KnowledgeQuery.Trim(),
            knowledgeTop,
            topChunks);

        // Load pre-extracted quick facts for the sources that returned chunks
        IReadOnlyCollection<KnowledgeQuickFactsSummary>? quickFacts = null;
        try
        {
            var sourceIds = topChunks.Select(c => c.SourceId).Distinct().ToArray();
            if (sourceIds.Length > 0)
            {
                var rawFacts = await _quickFactsRepository.GetBySourceIdsAsync(
                    query.TenantId,
                    query.SiteId,
                    sourceIds,
                    cancellationToken);

                if (rawFacts.Count > 0)
                {
                    quickFacts = rawFacts
                        .Select(f => new KnowledgeQuickFactsSummary(
                            f.SourceId,
                            f.ServicesOffered,
                            f.PricingSignals,
                            f.LocationCoverage,
                            f.HoursAvailability,
                            f.TeamCredentials,
                            f.FaqsText,
                            f.UniqueSellingPoints,
                            f.ExtractedAtUtc))
                        .ToArray();
                }
            }
        }
        catch
        {
            // Optional source: fail-soft.
        }

        VisitorProfileSummary? visitorProfile = null;
        IReadOnlyCollection<TimelineItemSummary>? timelineSummary = null;

        if (resolvedVisitorId.HasValue)
        {
            try
            {
                var detail = await _getVisitorDetailHandler.HandleAsync(
                    new GetVisitorDetailQuery(query.TenantId, query.SiteId, resolvedVisitorId.Value),
                    cancellationToken);

                if (detail is not null)
                {
                    visitorProfile = new VisitorProfileSummary(
                        detail.VisitorId,
                        detail.FirstSeenAtUtc,
                        detail.LastSeenAtUtc,
                        detail.VisitCount,
                        detail.TotalPagesVisited,
                        detail.PrimaryEmail,
                        detail.DisplayName,
                        detail.Phone,
                        detail.Language,
                        detail.Platform);

                    foreach (var session in detail.RecentSessions)
                    {
                        if (!string.IsNullOrWhiteSpace(session.SessionId))
                        {
                            collectorSessionIds.Add(session.SessionId.Trim());
                        }
                    }

                    var timelineLimit = Clamp(query.TimelineLimit, 1, MaxTimelineLimit);
                    var timeline = await _getVisitorTimelineHandler.HandleAsync(
                        new VisitorTimelineQuery(query.TenantId, query.SiteId, resolvedVisitorId.Value, timelineLimit),
                        cancellationToken);

                    timelineSummary = timeline
                        .Take(timelineLimit)
                        .Select(item => new TimelineItemSummary(
                            item.OccurredAtUtc,
                            item.Type,
                            ToExcerpt(item.Url, ExcerptLength),
                            item.SessionId,
                            item.MetadataSummary))
                        .ToArray();
                }
            }
            catch
            {
                // Optional source: fail-soft.
            }
        }

        EngageSessionSummary? engageSummary = null;
        if (engageSession is not null)
        {
            try
            {
                var engageLimit = Clamp(query.EngageMessageLimit, 1, MaxEngageMessageLimit);
                var messages = await _chatMessageRepository.ListBySessionAsync(engageSession.Id, cancellationToken);

                engageSummary = new EngageSessionSummary(
                    engageSession.Id,
                    engageSession.CreatedAtUtc,
                    engageSession.UpdatedAtUtc,
                    messages
                        .OrderByDescending(item => item.CreatedAtUtc)
                        .Take(engageLimit)
                        .OrderBy(item => item.CreatedAtUtc)
                        .Select(item => new EngageMessageSummary(
                            item.Role,
                            ToExcerpt(item.Content, ExcerptLength),
                            item.CreatedAtUtc,
                            item.Confidence,
                            item.Citations?.Count ?? 0))
                        .ToArray());
            }
            catch
            {
                // Optional source: fail-soft.
            }
        }

        IReadOnlyCollection<TicketSummary>? ticketsSummary = null;
        try
        {
            var ticketLimit = Clamp(query.TicketsLimit, 1, MaxTicketsLimit);
            var tickets = await _listTicketsHandler.HandleAsync(
                new ListTicketsQuery(query.TenantId, query.SiteId, resolvedVisitorId, query.EngageSessionId, 1, ticketLimit),
                cancellationToken);

            ticketsSummary = tickets
                .Take(ticketLimit)
                .Select(item => new TicketSummary(
                    item.Id,
                    ToExcerpt(item.Subject, 120),
                    item.Status,
                    item.CreatedAtUtc,
                    item.UpdatedAtUtc,
                    item.EngageSessionId,
                    item.VisitorId))
                .ToArray();
        }
        catch
        {
            // Optional source: fail-soft.
        }

        IReadOnlyCollection<PromoInteractionSummary>? promoSummary = null;
        if (resolvedVisitorId.HasValue)
        {
            try
            {
                var promoLimit = Clamp(query.PromoEntriesLimit, 1, MaxPromoEntriesLimit);
                var entries = await _promoEntryRepository.ListByVisitorAsync(
                    new ListVisitorPromoEntriesQuery(query.TenantId, query.SiteId, resolvedVisitorId.Value, 1, promoLimit),
                    cancellationToken);

                promoSummary = entries
                    .Take(promoLimit)
                    .Select(item => new PromoInteractionSummary(
                        item.Id,
                        item.PromoId,
                        item.CreatedAtUtc,
                        item.Email,
                        item.Name,
                        ToAnswerHighlights(item.Answers)))
                    .ToArray();
            }
            catch
            {
                // Optional source: fail-soft.
            }
        }

        IntelligenceSnapshot? intelligenceSnapshot = null;
        if (query.IncludeIntelligenceSnapshot)
        {
            try
            {
                var dashboardResult = await _queryIntelligenceTrendsService.HandleDashboardAsync(
                    query.TenantId.ToString("D"),
                    new IntelligenceDashboardQuery(
                        query.SiteId,
                        query.IntelligenceCategory,
                        query.IntelligenceLocation,
                        query.IntelligenceTimeWindow,
                        query.IntelligenceProvider,
                        query.IntelligenceKeyword,
                        query.IntelligenceAudienceType,
                        query.IntelligenceLimit),
                    cancellationToken);

                if (dashboardResult.Status == OperationStatus.Success && dashboardResult.Value is not null)
                {
                    var dashboard = dashboardResult.Value;
                    intelligenceSnapshot = new IntelligenceSnapshot(
                        dashboard.Category,
                        dashboard.Location,
                        dashboard.TimeWindow,
                        dashboard.Provider,
                        dashboard.RefreshedAtUtc,
                        dashboard.TotalItems,
                        dashboard.Summary,
                        dashboard.TopItems);
                }
            }
            catch
            {
                // Optional source: fail-soft.
            }
        }

        var contextRef = new AiDecisionContextRef(
            query.TenantId,
            query.SiteId,
            resolvedVisitorId,
            query.EngageSessionId);

        var bundle = new VisitorContextBundle(
            contextRef,
            collectorSessionIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            knowledgeSnapshot,
            visitorProfile,
            timelineSummary,
            engageSummary,
            ticketsSummary,
            promoSummary,
            intelligenceSnapshot,
            quickFacts);

        return OperationResult<VisitorContextBundle>.Success(bundle);
    }

    private static ValidationErrors Validate(BuildVisitorContextBundleQuery query)
    {
        var errors = new ValidationErrors();

        if (query.TenantId == Guid.Empty)
        {
            errors.Add("tenantId", "Tenant id is required.");
        }

        if (query.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (query.VisitorId is null && query.EngageSessionId is null)
        {
            errors.Add("context", "Either visitorId or engageSessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(query.KnowledgeQuery))
        {
            errors.Add("knowledgeQuery", "Knowledge query is required.");
        }

        return errors;
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : (value > max ? max : value);

    private static string ToExcerpt(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static IReadOnlyDictionary<string, string>? ToAnswerHighlights(IReadOnlyDictionary<string, string>? answers)
    {
        if (answers is null || answers.Count == 0)
        {
            return null;
        }

        var highlights = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in answers)
        {
            if (highlights.Count == 5)
            {
                break;
            }

            var normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            highlights[normalizedKey] = ToExcerpt(value, 100);
        }

        return highlights.Count == 0 ? null : highlights;
    }
}
