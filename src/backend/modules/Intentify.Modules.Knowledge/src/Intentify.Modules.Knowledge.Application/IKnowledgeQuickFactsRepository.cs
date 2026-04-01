using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeQuickFactsRepository
{
    Task UpsertAsync(KnowledgeQuickFacts quickFacts, CancellationToken cancellationToken = default);

    Task<KnowledgeQuickFacts?> GetBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeQuickFacts>> GetBySourceIdsAsync(
        Guid tenantId,
        Guid siteId,
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken = default);

    Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default);
}
