using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageBotRepository
{
    Task<EngageBot> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<EngageBot?> GetBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EngageBotDigestInfo>> ListDigestEnabledBotsAsync(CancellationToken ct = default);
    Task<EngageBot?> UpdateSettingsAsync(Guid tenantId, Guid siteId, string name, string? primaryColor, bool? launcherVisible, string? tone, string? verbosity, string? fallbackStyle, string? businessDescription, string? industry, string? servicesDescription, string? geoFocus, string? personalityDescriptor, bool digestEmailEnabled, string? digestEmailRecipients, string? digestEmailFrequency, bool hideBranding = false, string? customBrandingText = null, bool abTestEnabled = false, string? openingMessageA = null, string? openingMessageB = null, bool surveyEnabled = false, string? surveyQuestion = null, string? surveyOptions = null, bool exitIntentEnabled = false, string? exitIntentMessage = null, CancellationToken cancellationToken = default);
    Task IncrementAbTestImpressionAsync(Guid tenantId, Guid siteId, string variant, CancellationToken cancellationToken = default);
    Task IncrementAbTestConversionAsync(Guid tenantId, Guid siteId, string variant, CancellationToken cancellationToken = default);
    Task ResetAbTestCountersAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
