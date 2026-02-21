using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageChatSessionRepository
{
    Task<EngageChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task InsertAsync(EngageChatSession session, CancellationToken cancellationToken = default);
    Task TouchAsync(Guid sessionId, DateTime timestampUtc, CancellationToken cancellationToken = default);
    Task SetCollectorSessionIdIfEmptyAsync(Guid sessionId, string collectorSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EngageChatSession>> ListBySiteAsync(Guid tenantId, Guid siteId, string? collectorSessionId = null, CancellationToken cancellationToken = default);
}
