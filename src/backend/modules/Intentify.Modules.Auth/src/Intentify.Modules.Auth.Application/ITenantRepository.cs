using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task InsertAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateNameAsync(Guid id, string name, DateTime updatedAt, CancellationToken cancellationToken = default);
}
