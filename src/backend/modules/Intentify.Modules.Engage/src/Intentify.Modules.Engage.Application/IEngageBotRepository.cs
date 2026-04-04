using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageBotRepository
{
    Task<EngageBot> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<EngageBot?> GetBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<EngageBot?> UpdateSettingsAsync(Guid tenantId, Guid siteId, string name, string? primaryColor, bool? launcherVisible, string? tone, string? verbosity, string? fallbackStyle, string? businessDescription, string? industry, string? servicesDescription, string? geoFocus, string? personalityDescriptor, bool digestEmailEnabled, string? digestEmailRecipients, string? digestEmailFrequency, CancellationToken cancellationToken = default);
}
