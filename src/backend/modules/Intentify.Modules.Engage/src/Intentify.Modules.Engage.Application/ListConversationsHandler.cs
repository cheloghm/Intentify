namespace Intentify.Modules.Engage.Application;

public sealed class ListConversationsHandler
{
    private readonly IEngageChatSessionRepository _sessionRepository;

    public ListConversationsHandler(IEngageChatSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyCollection<ConversationSummaryResult>> HandleAsync(ListConversationsQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.ListBySiteAsync(query.TenantId, query.SiteId, query.CollectorSessionId, cancellationToken);
        return sessions
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new ConversationSummaryResult(item.Id, item.CreatedAtUtc, item.UpdatedAtUtc))
            .ToArray();
    }
}

public sealed class GetOpportunityAnalyticsHandler
{
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageHandoffTicketRepository _handoffTicketRepository;

    public GetOpportunityAnalyticsHandler(
        IEngageChatSessionRepository sessionRepository,
        IEngageHandoffTicketRepository handoffTicketRepository)
    {
        _sessionRepository = sessionRepository;
        _handoffTicketRepository = handoffTicketRepository;
    }

    public async Task<OpportunityAnalyticsResult> HandleAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.ListBySiteAsync(tenantId, siteId, null, cancellationToken);
        var handoffs = await _handoffTicketRepository.ListBySiteAsync(tenantId, siteId, cancellationToken);

        var commercialSessionIds = handoffs
            .Where(item => string.Equals(item.Reason, "CommercialOpportunity", StringComparison.Ordinal))
            .Select(item => item.SessionId)
            .ToHashSet();

        var supportSessionIds = handoffs
            .Where(item => string.Equals(item.Reason, "NeedsHumanHelp", StringComparison.Ordinal)
                || string.Equals(item.Reason, "ContactDetails", StringComparison.Ordinal))
            .Select(item => item.SessionId)
            .ToHashSet();

        var totalCommercialOpportunities = commercialSessionIds.Count;
        var commercialCount = totalCommercialOpportunities;
        var supportCount = supportSessionIds.Count;
        var generalCount = Math.Max(0, sessions.Count - commercialSessionIds.Union(supportSessionIds).Count());

        var commercialSessions = sessions
            .Where(item => commercialSessionIds.Contains(item.Id))
            .ToArray();

        var highIntentCount = commercialSessions.Count(item => (item.IntentScore ?? 0) >= 80);

        var emailCount = commercialSessions.Count(item => string.Equals(item.CapturedPreferredContactMethod, "Email", StringComparison.Ordinal));
        var phoneCount = commercialSessions.Count(item => string.Equals(item.CapturedPreferredContactMethod, "Phone", StringComparison.Ordinal));
        var unknownCount = Math.Max(0, commercialSessions.Length - emailCount - phoneCount);

        var overTime = commercialSessions
            .GroupBy(item => item.CreatedAtUtc.Date)
            .OrderBy(item => item.Key)
            .Select(item => new OpportunityDailyPointResult(item.Key, item.Count()))
            .ToArray();

        return new OpportunityAnalyticsResult(
            totalCommercialOpportunities,
            commercialCount,
            supportCount,
            generalCount,
            highIntentCount,
            new OpportunityContactMethodBreakdownResult(emailCount, phoneCount, unknownCount),
            overTime);
    }
}
