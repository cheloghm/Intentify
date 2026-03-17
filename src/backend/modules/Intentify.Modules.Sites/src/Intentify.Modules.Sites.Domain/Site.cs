using MongoDB.Bson.Serialization.Attributes;

namespace Intentify.Modules.Sites.Domain;

public sealed class Site
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public string Domain { get; init; } = string.Empty;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string> AllowedOrigins { get; init; } = [];

    // Keys are stored in plain text for MVP purposes; keep them high entropy and never log them.
    public string SiteKey { get; init; } = string.Empty;

    public string WidgetKey { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime? FirstEventReceivedAtUtc { get; init; }
}
