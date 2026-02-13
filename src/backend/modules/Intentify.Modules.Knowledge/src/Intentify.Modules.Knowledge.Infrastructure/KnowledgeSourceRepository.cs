using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class KnowledgeSourceRepository : IKnowledgeSourceRepository
{
    private readonly IMongoCollection<KnowledgeSource> _sources;
    private readonly Task _ensureIndexes;

    public KnowledgeSourceRepository(IMongoDatabase database)
    {
        _sources = database.GetCollection<KnowledgeSource>(KnowledgeMongoCollections.Sources);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _sources.InsertOneAsync(source, cancellationToken: cancellationToken);
    }

    public async Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sources.Find(item => item.TenantId == tenantId && item.Id == sourceId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _sources.Find(item => item.TenantId == tenantId && item.SiteId == siteId)
            .SortByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var update = Builders<KnowledgeSource>.Update
            .Set(item => item.Status, status)
            .Set(item => item.FailureReason, failureReason)
            .Set(item => item.IndexedAtUtc, indexedAtUtc)
            .Set(item => item.UpdatedAtUtc, DateTime.UtcNow);

        await _sources.UpdateOneAsync(
            item => item.TenantId == tenantId && item.Id == sourceId,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        var update = Builders<KnowledgeSource>.Update
            .Set(item => item.PdfBytes, pdfBytes)
            .Set(item => item.Status, status)
            .Set(item => item.FailureReason, null)
            .Set(item => item.IndexedAtUtc, null)
            .Set(item => item.UpdatedAtUtc, updatedAtUtc);

        await _sources.UpdateOneAsync(
            item => item.TenantId == tenantId && item.Id == sourceId,
            update,
            cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<KnowledgeSource>(Builders<KnowledgeSource>.IndexKeys
                .Ascending(item => item.TenantId)
                .Ascending(item => item.SiteId)),
            new CreateIndexModel<KnowledgeSource>(Builders<KnowledgeSource>.IndexKeys.Ascending(item => item.Status)),
            new CreateIndexModel<KnowledgeSource>(Builders<KnowledgeSource>.IndexKeys.Ascending(item => item.Id))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_sources, indexes);
    }
}
