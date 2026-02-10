using Intentify.Modules.Sites.Domain;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class RotateKeysHandler
{
    private readonly ISiteRepository _sites;
    private readonly IKeyGenerator _keyGenerator;

    public RotateKeysHandler(ISiteRepository sites, IKeyGenerator keyGenerator)
    {
        _sites = sites;
        _keyGenerator = keyGenerator;
    }

    public async Task<OperationResult<Site>> HandleAsync(RotateKeysCommand command, CancellationToken cancellationToken = default)
    {
        var newSiteKey = _keyGenerator.GenerateKey(KeyPurpose.SiteKey);
        var newWidgetKey = _keyGenerator.GenerateKey(KeyPurpose.WidgetKey);

        var updated = await _sites.RotateKeysAsync(command.TenantId, command.SiteId, newSiteKey, newWidgetKey, cancellationToken);
        if (updated is null)
        {
            return OperationResult<Site>.NotFound();
        }

        return OperationResult<Site>.Success(updated);
    }
}
