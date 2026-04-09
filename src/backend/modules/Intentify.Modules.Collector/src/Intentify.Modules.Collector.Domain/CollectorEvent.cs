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

    public string? IpAddress { get; init; }

    public string? Country { get; init; }

    public string? City { get; init; }

    public string? Region { get; init; }

    public bool CrossOriginEvent { get; init; }

    // Page / product metadata (extracted from pageMeta in event data)
    public string? PageType { get; init; }
    public string? ProductName { get; init; }
    public string? ProductPrice { get; init; }
    public string? ProductBrand { get; init; }
    public string? ProductCategory { get; init; }
    public string? ProductSku { get; init; }
    public bool? ProductAvailable { get; init; }
    public string? OgType { get; init; }
}
