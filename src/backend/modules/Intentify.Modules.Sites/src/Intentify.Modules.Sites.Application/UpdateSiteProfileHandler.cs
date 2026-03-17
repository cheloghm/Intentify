using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class UpdateSiteProfileHandler
{
    private readonly ISiteRepository _sites;

    public UpdateSiteProfileHandler(ISiteRepository sites)
    {
        _sites = sites;
    }

    public async Task<OperationResult<Site>> HandleAsync(UpdateSiteProfileCommand command, CancellationToken cancellationToken = default)
    {
        var site = await _sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<Site>.NotFound();
        }

        var tags = (command.Tags ?? Array.Empty<string>())
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var errors = new ValidationErrors();
        var normalizedDomain = site.Domain;
        if (!string.IsNullOrWhiteSpace(command.Domain))
        {
            if (!DomainNormalizer.TryNormalize(command.Domain, out var parsedDomain))
            {
                errors.Add("domain", "Domain must be a valid hostname (localhost allowed).");
                return OperationResult<Site>.ValidationFailed(errors);
            }

            normalizedDomain = parsedDomain;
        }

        if (!string.Equals(normalizedDomain, site.Domain, StringComparison.Ordinal))
        {
            var duplicate = await _sites.GetByTenantAndDomainAsync(command.TenantId, normalizedDomain, cancellationToken);
            if (duplicate is not null && duplicate.Id != command.SiteId)
            {
                return OperationResult<Site>.Conflict();
            }
        }

        var updated = await _sites.UpdateProfileAsync(
            command.TenantId,
            command.SiteId,
            NormalizeText(command.Name) ?? site.Name,
            normalizedDomain,
            NormalizeText(command.Description),
            NormalizeText(command.Category),
            tags,
            cancellationToken);

        return updated is null
            ? OperationResult<Site>.NotFound()
            : OperationResult<Site>.Success(updated);
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
