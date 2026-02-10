using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class GetPublicInstallationStatusHandler
{
    private readonly ISiteRepository _sites;

    public GetPublicInstallationStatusHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<PublicInstallationStatusResult>> HandleAsync(
        GetPublicInstallationStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(command.WidgetKey))
        {
            errors.Add("widgetKey", "Widget key is required.");
            return OperationResult<PublicInstallationStatusResult>.ValidationFailed(errors);
        }

        if (!OriginNormalizer.TryNormalize(command.Origin, out var normalizedOrigin))
        {
            errors.Add("origin", "Origin or Referer header is required to determine the request origin.");
            return OperationResult<PublicInstallationStatusResult>.ValidationFailed(errors);
        }

        var site = await _sites.GetByWidgetKeyAsync(command.WidgetKey.Trim(), cancellationToken);
        if (site is null)
        {
            return OperationResult<PublicInstallationStatusResult>.NotFound();
        }

        if (!site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return OperationResult<PublicInstallationStatusResult>.Forbidden();
        }

        return OperationResult<PublicInstallationStatusResult>.Success(new PublicInstallationStatusResult(site, normalizedOrigin));
    }
}
