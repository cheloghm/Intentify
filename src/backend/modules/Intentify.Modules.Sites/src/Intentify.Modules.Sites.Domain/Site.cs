using MongoDB.Bson.Serialization.Attributes;

namespace Intentify.Modules.Sites.Domain;

public sealed class Site
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Domain { get; init; } = string.Empty;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string> AllowedOrigins { get; init; } = [];

    // Keys are stored in plain text for MVP purposes; keep them high entropy and never log them.
    public string SiteKey { get; init; } = string.Empty;

    public string WidgetKey { get; init; } = string.Empty;

    /// <summary>Stable opaque token used to identify this site in the tracker snippet.</summary>
    public Guid SnippetId { get; init; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime? FirstEventReceivedAtUtc { get; init; }

    // ── Phase 7.2: REST API Keys ──────────────────────────────────────────────
    /// <summary>
    /// Named REST API keys allowing enterprise clients to pull data programmatically.
    /// Raw secrets are never stored — only the SHA-256 hash.
    /// </summary>
    public List<SiteApiKey> ApiKeys { get; set; } = [];
}

/// <summary>
/// One named REST API key attached to a site.
/// The raw secret is returned once at creation and never again.
/// </summary>
public sealed class SiteApiKey
{
    /// <summary>Opaque stable identifier for this key (used to revoke).</summary>
    public string KeyId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable label so tenants can identify the key.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest of the raw secret. Never return this over the API.</summary>
    public string SecretHash { get; init; } = string.Empty;

    /// <summary>First 8 chars of the raw secret shown as a hint (e.g. "itfy_abc…").</summary>
    public string Hint { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Null until the key is revoked.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null;
}
