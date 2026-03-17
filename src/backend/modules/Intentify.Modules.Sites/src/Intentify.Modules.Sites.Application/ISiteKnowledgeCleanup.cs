namespace Intentify.Modules.Sites.Application;

public interface ISiteKnowledgeCleanup
{
    Task CleanupSiteKnowledgeAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
