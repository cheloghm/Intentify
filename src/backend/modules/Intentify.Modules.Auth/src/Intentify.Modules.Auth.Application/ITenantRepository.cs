using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface ITenantRepository
{
    Task<Tenant?> GetFirstAsync(CancellationToken cancellationToken = default);
    Task InsertAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
