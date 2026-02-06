using MongoDB.Bson.Serialization.Attributes;

namespace Intentify.Modules.Auth.Domain;

public sealed class Tenant
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public string Domain { get; init; } = string.Empty;

    public string Plan { get; init; } = string.Empty;

    public string Industry { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
