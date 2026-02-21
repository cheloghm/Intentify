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
