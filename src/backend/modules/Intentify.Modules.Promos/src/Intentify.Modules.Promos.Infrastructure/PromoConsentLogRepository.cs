using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Promos.Infrastructure;

public sealed class PromoConsentLogRepository : IPromoConsentLogRepository
{
    private readonly IMongoCollection<PromoConsentLog> _logs;
    private readonly Task _ensureIndexes;
    public PromoConsentLogRepository(IMongoDatabase database)
    {
        _logs = database.GetCollection<PromoConsentLog>(PromosMongoCollections.PromoConsentLogs);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task InsertAsync(PromoConsentLog consentLog, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        await _logs.InsertOneAsync(consentLog, cancellationToken: cancellationToken);
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<PromoConsentLog>(Builders<PromoConsentLog>.IndexKeys.Ascending(item => item.TenantId).Ascending(item => item.PromoEntryId))
        };
        return MongoIndexHelper.EnsureIndexesAsync(_logs, indexes);
    }
}
