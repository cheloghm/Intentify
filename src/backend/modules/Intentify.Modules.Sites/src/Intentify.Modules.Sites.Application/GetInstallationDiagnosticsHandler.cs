using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed record GetInstallationDiagnosticsCommand(Guid TenantId, Guid SiteId, string? SiteKey, string? WidgetKey, string? Origin);

public sealed record InstallationDiagnosticsResult(
    Site Site,
    bool SiteKeyValid,
    bool WidgetKeyValid,
    string? NormalizedOrigin,
    bool OriginAllowed,
    bool TrackerScriptExpected,
    bool WidgetScriptExpected,
    bool FirstEventSeen);

public sealed class GetInstallationDiagnosticsHandler
{
    private readonly ISiteRepository _sites;

    public GetInstallationDiagnosticsHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<InstallationDiagnosticsResult>> HandleAsync(
        GetInstallationDiagnosticsCommand command,
        CancellationToken cancellationToken = default)
    {
        var site = await _sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<InstallationDiagnosticsResult>.NotFound();
        }

        var normalizedSiteKey = NormalizeOptional(command.SiteKey);
        var normalizedWidgetKey = NormalizeOptional(command.WidgetKey);

        var siteKeyValid = string.IsNullOrWhiteSpace(normalizedSiteKey)
            ? !string.IsNullOrWhiteSpace(site.SiteKey)
            : string.Equals(site.SiteKey, normalizedSiteKey, StringComparison.Ordinal);

        var widgetKeyValid = string.IsNullOrWhiteSpace(normalizedWidgetKey)
            ? !string.IsNullOrWhiteSpace(site.WidgetKey)
            : string.Equals(site.WidgetKey, normalizedWidgetKey, StringComparison.Ordinal);

        var hasOrigin = OriginNormalizer.TryNormalize(command.Origin, out var normalizedOrigin);
        var originAllowed = hasOrigin && site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);

        var result = new InstallationDiagnosticsResult(
            site,
            siteKeyValid,
            widgetKeyValid,
            hasOrigin ? normalizedOrigin : null,
            originAllowed,
            TrackerScriptExpected: true,
            WidgetScriptExpected: true,
            FirstEventSeen: site.FirstEventReceivedAtUtc is not null);

        return OperationResult<InstallationDiagnosticsResult>.Success(result);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
