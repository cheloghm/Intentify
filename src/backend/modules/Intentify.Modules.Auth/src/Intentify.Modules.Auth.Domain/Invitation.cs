using MongoDB.Bson.Serialization.Attributes;

namespace Intentify.Modules.Auth.Domain;

public sealed class Invitation
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = AuthRoles.User;

    public string Token { get; init; } = string.Empty;

    public Guid CreatedByUserId { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime? AcceptedAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
