using Intentify.Modules.Knowledge.Domain;

namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeSourceRepository
{
    Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default);

    Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid tenantId,
        Guid sourceId,
        IndexStatus status,
        string? failureReason,
        DateTime? indexedAtUtc,
        CancellationToken cancellationToken = default);

    Task ReplaceSourceContentAsync(
        Guid tenantId,
        Guid sourceId,
        byte[] pdfBytes,
        IndexStatus status,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);
}
