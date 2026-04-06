namespace Intentify.Modules.Sites.Api;

public sealed record CreateSiteRequest(string Domain, string? Description = null, string? Category = null, IReadOnlyCollection<string>? Tags = null, string? Name = null);

public sealed record UpdateSiteProfileRequest(string? Name, string? Domain, string? Description, string? Category, IReadOnlyCollection<string>? Tags);

public sealed record CreateSiteResponse(
    string SiteId,
    string Name,
    string Domain,
    string? Description,
    string? Category,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> AllowedOrigins,
    string SiteKey,
    string WidgetKey);

public sealed record SiteSummaryResponse(
    string SiteId,
    string Name,
    string Domain,
    string? Description,
    string? Category,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> AllowedOrigins,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    InstallationStatusResponse InstallationStatus);

public sealed record UpdateAllowedOriginsRequest(IReadOnlyCollection<string> AllowedOrigins);

public sealed record RegenerateKeysResponse(string SiteKey, string WidgetKey);

public sealed record SiteKeysResponse(string SiteKey, string WidgetKey);

public sealed record InstallationStatusResponse(
    string SiteId,
    string Domain,
    bool IsConfigured,
    int AllowedOriginsCount,
    bool IsInstalled,
    DateTime? FirstEventReceivedAtUtc);

public sealed record InstallationDiagnosticsResponse(
    string SiteId,
    string Domain,
    bool SiteKeyValid,
    string? Origin,
    bool OriginAllowed,
    bool SdkScriptExpected,
    bool FirstEventSeen,
    DateTime? FirstEventReceivedAtUtc);

// ── Phase 7.2: REST API Key Management ────────────────────────────────────────

/// <summary>Request body when creating a new API key.</summary>
public sealed record GenerateApiKeyRequest(string Label);

/// <summary>
/// Returned once when a key is generated. RawSecret will never be returned again.
/// The client must copy it immediately.
/// </summary>
public sealed record GenerateApiKeyResponse(
    string KeyId,
    string Label,
    string RawSecret,
    string Hint,
    DateTime CreatedAtUtc);

/// <summary>Safe read-only view of a key — never exposes the secret or hash.</summary>
public sealed record ApiKeyResponse(
    string KeyId,
    string Label,
    string Hint,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    bool IsActive);
