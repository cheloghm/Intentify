using Intentify.Modules.Sites.Domain;

namespace Intentify.Modules.Sites.Application;

public interface ISiteRepository
{
    Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default);
    Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<Site?> GetByWidgetKeyAsync(string widgetKey, CancellationToken cancellationToken = default);
    Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Site>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> TenantHasSiteAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(Site site, CancellationToken cancellationToken = default);
    Task<Site?> UpdateProfileAsync(Guid tenantId, Guid siteId, string? description, string? category, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default);
    Task<Site?> UpdateAllowedOriginsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<string> allowedOrigins, CancellationToken cancellationToken = default);
    Task<Site?> RotateKeysAsync(Guid tenantId, Guid siteId, string siteKey, string widgetKey, CancellationToken cancellationToken = default);
    Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default);
}
