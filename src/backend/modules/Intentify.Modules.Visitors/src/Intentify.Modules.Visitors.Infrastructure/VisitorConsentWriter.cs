using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Visitors.Infrastructure;

public sealed class VisitorConsentWriter : IVisitorConsentWriter
{
    private readonly IMongoCollection<Visitor> _visitors;

    public VisitorConsentWriter(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
    }

    public async Task<RecordVisitorConsentResult> RecordConsentAsync(
        RecordVisitorConsentCommand command,
        CancellationToken cancellationToken = default)
    {
        var decision = new VisitorConsentDecision
        {
            DecidedAtUtc  = DateTime.UtcNow,
            ConsentGiven  = command.ConsentGiven,
            Version       = command.Version,
        };

        var filter = Builders<Visitor>.Filter.Eq(v => v.Id, command.VisitorId)
            & Builders<Visitor>.Filter.Eq(v => v.SiteId, command.SiteId)
            & Builders<Visitor>.Filter.Eq(v => v.TenantId, command.TenantId);

        var update = Builders<Visitor>.Update
            .Push(v => v.ConsentDecisions, decision)
            .Set(v => v.LatestConsentGiven, command.ConsentGiven)
            .Set(v => v.LatestConsentAtUtc, decision.DecidedAtUtc);

        // When consent is withdrawn also anonymise PII so data is scrubbed immediately.
        if (!command.ConsentGiven)
        {
            update = update
                .Set(v => v.PrimaryEmail,  null)
                .Set(v => v.DisplayName,   null)
                .Set(v => v.Phone,         null)
                .Set(v => v.IsAnonymised,  true);
        }

        var result = await _visitors.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return new RecordVisitorConsentResult(result.MatchedCount > 0);
    }
}
