namespace Intentify.Modules.Sites.Application;

public sealed record CreateSiteCommand(Guid TenantId, string Domain, string? Description, string? Category, IReadOnlyCollection<string>? Tags, string? Name = null);

public sealed record UpdateSiteProfileCommand(Guid TenantId, Guid SiteId, string? Name, string? Domain, string? Description, string? Category, IReadOnlyCollection<string>? Tags);

public sealed record DeleteSiteCommand(Guid TenantId, Guid SiteId);

public sealed record UpdateAllowedOriginsCommand(Guid TenantId, Guid SiteId, IReadOnlyCollection<string> AllowedOrigins);

public sealed record RotateKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetSiteKeysCommand(Guid TenantId, Guid SiteId);

public sealed record GetInstallationStatusCommand(Guid TenantId, Guid SiteId);

public sealed record GetPublicInstallationStatusCommand(string WidgetKey, string? Origin);
