namespace Intentify.Modules.Sites.Application;

public sealed record CreateSiteCommand(Guid TenantId, string Domain, string? Description, string? Category, IReadOnlyCollection<string>? Tags, string? Name = null, string? Plan = null);

public sealed record UpdateSiteProfileCommand(Guid TenantId, Guid SiteId, string? Name, string? Domain, string? Description, string? Category, IReadOnlyCollection<string>? Tags);

public sealed record DeleteSiteCommand(Guid TenantId, Guid SiteId);

public sealed record UpdateAllowedOriginsCommand(Guid TenantId, Guid SiteId, IReadOnlyCollection<string> AllowedOrigins);

public sealed record RotateKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetSiteKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetInstallationStatusCommand(Guid TenantId, Guid SiteId);

public sealed record GetPublicInstallationStatusCommand(string WidgetKey, string? Origin);

// ── Phase 7.2: REST API Key Management ────────────────────────────────────────

/// <summary>
/// Generates a new named REST API key for a site.
/// Keys are prefixed "itfy_" and stored hashed; the raw value is returned only once.
/// </summary>
public sealed record GenerateApiKeyCommand(Guid TenantId, Guid SiteId, string Label);

/// <summary>
/// Revokes an existing REST API key by its key ID.
/// </summary>
public sealed record RevokeApiKeyCommand(Guid TenantId, Guid SiteId, string KeyId);

/// <summary>
/// Lists all non-revoked API keys for a site (label + id + createdAt; never the raw secret).
/// </summary>
public sealed record ListApiKeysCommand(Guid TenantId, Guid SiteId);
