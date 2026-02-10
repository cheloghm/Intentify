using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class UpdateAllowedOriginsHandler
{
    private readonly ISiteRepository _sites;

    public UpdateAllowedOriginsHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<Site>> HandleAsync(UpdateAllowedOriginsCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command, out var normalizedOrigins);
        if (errors.HasErrors)
        {
            return OperationResult<Site>.ValidationFailed(errors);
        }

        var updated = await _sites.UpdateAllowedOriginsAsync(command.TenantId, command.SiteId, normalizedOrigins!, cancellationToken);
        if (updated is null)
        {
            return OperationResult<Site>.NotFound();
        }

        return OperationResult<Site>.Success(updated);
    }

    private static ValidationErrors Validate(UpdateAllowedOriginsCommand command, out IReadOnlyCollection<string>? normalizedOrigins)
    {
        var errors = new ValidationErrors();
        normalizedOrigins = null;

        if (command.AllowedOrigins is null)
        {
            errors.Add("allowedOrigins", "Allowed origins are required.");
            return errors;
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in command.AllowedOrigins)
        {
            if (!OriginNormalizer.TryNormalize(origin, out var normalizedOrigin))
            {
                errors.Add("allowedOrigins", "Allowed origins must be valid absolute HTTP/HTTPS origins without paths.");
                return errors;
            }

            if (!normalized.Add(normalizedOrigin))
            {
                errors.Add("allowedOrigins", "Allowed origins must not contain duplicates.");
                return errors;
            }
        }

        normalizedOrigins = normalized.ToList();
        return errors;
    }
}
