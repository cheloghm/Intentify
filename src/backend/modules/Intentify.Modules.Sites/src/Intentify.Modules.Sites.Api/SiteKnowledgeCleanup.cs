using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Api;

public sealed class SiteKnowledgeCleanup : ISiteKnowledgeCleanup
{
    private readonly IKnowledgeSourceRepository _sources;
    private readonly DeleteKnowledgeSourceHandler _deleteSourceHandler;

    public SiteKnowledgeCleanup(IKnowledgeSourceRepository sources, DeleteKnowledgeSourceHandler deleteSourceHandler)
    {
        _sources = sources;
        _deleteSourceHandler = deleteSourceHandler;
    }

    public async Task CleanupSiteKnowledgeAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var sources = await _sources.ListSourcesAsync(tenantId, siteId, cancellationToken);
        foreach (var source in sources)
        {
            var result = await _deleteSourceHandler.HandleAsync(new DeleteKnowledgeSourceCommand(tenantId, source.Id), cancellationToken);
            if (result.Status is not OperationStatus.Success and not OperationStatus.NotFound)
            {
                throw new InvalidOperationException($"Failed to delete knowledge source {source.Id} for site {siteId}.");
            }
        }
    }
}
