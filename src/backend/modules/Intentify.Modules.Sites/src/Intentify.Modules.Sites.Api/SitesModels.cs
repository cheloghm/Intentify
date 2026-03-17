namespace Intentify.Modules.Sites.Api;

public sealed record CreateSiteRequest(string Domain);

public sealed record CreateSiteResponse(
    string SiteId,
    string Domain,
    IReadOnlyCollection<string> AllowedOrigins,
    string SiteKey,
    string WidgetKey);

public sealed record SiteSummaryResponse(
    string SiteId,
    string Domain,
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
