using Intentify.Shared.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Intentify.Modules.Sites.Application;

public sealed class GetPublicInstallationStatusHandler
{
    private readonly ISiteRepository _sites;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public GetPublicInstallationStatusHandler(
        ISiteRepository sites,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _sites = sites;
        _environment = environment;
        _configuration = configuration;
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

        if (!site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase) &&
            !CanBypassOriginValidation(normalizedOrigin))
        {
            return OperationResult<PublicInstallationStatusResult>.Forbidden();
        }

        return OperationResult<PublicInstallationStatusResult>.Success(new PublicInstallationStatusResult(site, normalizedOrigin));
    }

    private bool CanBypassOriginValidation(string normalizedOrigin)
    {
        if (!_environment.IsDevelopment())
        {
            var allowLocalhost = _configuration.GetValue<bool>("Intentify:Sites:AllowLocalhostInstallStatus");
            if (!allowLocalhost)
            {
                return false;
            }
        }

        if (!Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1";
    }
}
