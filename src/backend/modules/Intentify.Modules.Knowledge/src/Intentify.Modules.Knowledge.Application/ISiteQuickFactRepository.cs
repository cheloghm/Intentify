using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface ISiteQuickFactRepository
{
    Task<IReadOnlyCollection<SiteQuickFact>> ListAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task InsertAsync(SiteQuickFact fact, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid siteId, Guid factId, CancellationToken cancellationToken = default);
}
