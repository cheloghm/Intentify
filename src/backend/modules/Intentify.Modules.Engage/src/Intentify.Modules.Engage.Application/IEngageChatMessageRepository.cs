using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageChatMessageRepository
{
    Task InsertAsync(EngageChatMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EngageChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EngageChatMessage>> ListBySessionAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken = default);
}
