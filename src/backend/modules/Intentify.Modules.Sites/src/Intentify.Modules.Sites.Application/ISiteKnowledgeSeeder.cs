namespace Intentify.Modules.Sites.Application;

/// <summary>
/// Seeds default knowledge sources when a new site is created.
/// Implemented by the Knowledge module to avoid a circular project reference.
/// </summary>
public interface ISiteKnowledgeSeeder
{
    Task SeedDefaultSourcesAsync(
        Guid tenantId,
        Guid siteId,
        string domain,
        CancellationToken cancellationToken = default);
}
