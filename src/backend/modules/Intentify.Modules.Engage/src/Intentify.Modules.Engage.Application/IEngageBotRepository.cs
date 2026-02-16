using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageBotRepository
{
    Task<EngageBot> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<EngageBot?> GetBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
