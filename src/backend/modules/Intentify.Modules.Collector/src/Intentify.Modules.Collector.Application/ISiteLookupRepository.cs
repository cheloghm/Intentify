using Intentify.Modules.Sites.Domain;

namespace Intentify.Modules.Collector.Application;

public interface ISiteLookupRepository
{
    Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default);
    Task<Site?> GetBySnippetIdAsync(Guid snippetId, CancellationToken cancellationToken = default);
    Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default);
}
