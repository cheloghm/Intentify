using Intentify.Modules.LinkHub.Domain;

namespace Intentify.Modules.LinkHub.Application;

public interface ILinkHubRepository
{
    Task<LinkHubProfile?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<LinkHubProfile?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken ct = default);
    Task UpsertAsync(LinkHubProfile profile, CancellationToken ct = default);
    Task RecordClickAsync(LinkHubClick click, CancellationToken ct = default);
    Task IncrementLinkClickAsync(Guid profileId, string linkId, CancellationToken ct = default);
    Task<IReadOnlyList<LinkHubClick>> GetClicksAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
}
