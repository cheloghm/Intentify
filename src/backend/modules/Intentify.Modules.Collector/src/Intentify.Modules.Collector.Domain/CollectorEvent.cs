using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Intentify.Modules.Collector.Domain;

public sealed class CollectorEvent
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SiteId { get; init; }

    public Guid TenantId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string? Referrer { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public DateTime ReceivedAtUtc { get; init; }

    public string Origin { get; init; } = string.Empty;

    public string? SessionId { get; init; }

    public BsonDocument? Data { get; init; }
}
