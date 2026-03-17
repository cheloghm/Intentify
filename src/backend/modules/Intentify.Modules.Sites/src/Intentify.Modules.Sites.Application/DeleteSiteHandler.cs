using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class DeleteSiteHandler
{
    private readonly ISiteRepository _sites;
    private readonly ISiteKnowledgeCleanup _siteKnowledgeCleanup;

    public DeleteSiteHandler(ISiteRepository sites, ISiteKnowledgeCleanup siteKnowledgeCleanup)
    {
        _sites = sites;
        _siteKnowledgeCleanup = siteKnowledgeCleanup;
    }

    public async Task<OperationResult<bool>> HandleAsync(DeleteSiteCommand command, CancellationToken cancellationToken = default)
    {
        var site = await _sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<bool>.NotFound();
        }

        await _siteKnowledgeCleanup.CleanupSiteKnowledgeAsync(command.TenantId, command.SiteId, cancellationToken);
        var deleted = await _sites.DeleteAsync(command.TenantId, command.SiteId, cancellationToken);

        return deleted
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.NotFound();
    }
}
