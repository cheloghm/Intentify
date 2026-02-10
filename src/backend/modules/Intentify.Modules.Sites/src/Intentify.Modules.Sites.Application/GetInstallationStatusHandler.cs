using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class GetInstallationStatusHandler
{
    private readonly ISiteRepository _sites;

    public GetInstallationStatusHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<Site>> HandleAsync(GetInstallationStatusCommand command, CancellationToken cancellationToken = default)
    {
        var site = await _sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<Site>.NotFound();
        }

        return OperationResult<Site>.Success(site);
    }
}
