namespace Intentify.Modules.Sites.Application;

public sealed record CreateSiteCommand(Guid TenantId, string Domain);

public sealed record UpdateAllowedOriginsCommand(Guid TenantId, Guid SiteId, IReadOnlyCollection<string> AllowedOrigins);

public sealed record RotateKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetSiteKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetInstallationStatusCommand(Guid TenantId, Guid SiteId);

public sealed record GetPublicInstallationStatusCommand(string WidgetKey, string? Origin);
