namespace Intentify.Modules.Knowledge.Application;

public interface IEngageBotResolver
{
    Task<Guid> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
