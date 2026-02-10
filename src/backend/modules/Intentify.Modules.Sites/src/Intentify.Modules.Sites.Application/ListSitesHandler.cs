using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class ListSitesHandler
{
    private readonly ISiteRepository _sites;

    public ListSitesHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<IReadOnlyCollection<Site>>> HandleAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var results = await _sites.ListByTenantAsync(tenantId, cancellationToken);
        return OperationResult<IReadOnlyCollection<Site>>.Success(results);
    }
}
