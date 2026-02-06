using MongoDB.Bson.Serialization.Attributes;

namespace Intentify.Modules.Auth.Domain;

public sealed class User
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
