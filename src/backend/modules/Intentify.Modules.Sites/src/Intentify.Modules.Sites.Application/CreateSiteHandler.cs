using Intentify.Modules.Sites.Domain;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class CreateSiteHandler
{
    private readonly ISiteRepository _sites;
    private readonly IKeyGenerator _keyGenerator;

    public CreateSiteHandler(ISiteRepository sites, IKeyGenerator keyGenerator)
    {
        _sites = sites;
        _keyGenerator = keyGenerator;
    }

    public async Task<OperationResult<Site>> HandleAsync(CreateSiteCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var tags = NormalizeTags(command.Tags);

        if (string.IsNullOrWhiteSpace(command.Domain))
        {
            errors.Add("domain", "Domain is required.");
            return OperationResult<Site>.ValidationFailed(errors);
        }

        if (!DomainNormalizer.TryNormalize(command.Domain, out var normalizedDomain))
        {
            errors.Add("domain", "Domain must be a valid hostname (localhost allowed).");
            return OperationResult<Site>.ValidationFailed(errors);
        }

        var duplicate = await _sites.GetByTenantAndDomainAsync(command.TenantId, normalizedDomain, cancellationToken);
        if (duplicate is not null)
        {
            return OperationResult<Site>.Conflict();
        }

        if (await _sites.TenantHasSiteAsync(command.TenantId, cancellationToken))
        {
            errors.Add("site", "Tenant already has a site.");
            return OperationResult<Site>.ValidationFailed(errors);
        }

        var now = DateTime.UtcNow;
        var site = new Site
        {
            TenantId = command.TenantId,
            Name = NormalizeText(command.Name) ?? normalizedDomain,
            Domain = normalizedDomain,
            Description = NormalizeText(command.Description),
            Category = NormalizeText(command.Category),
            Tags = tags,
            AllowedOrigins = [],
            SiteKey = _keyGenerator.GenerateKey(KeyPurpose.SiteKey),
            WidgetKey = _keyGenerator.GenerateKey(KeyPurpose.WidgetKey),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sites.InsertAsync(site, cancellationToken);

        return OperationResult<Site>.Success(site);
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static List<string> NormalizeTags(IReadOnlyCollection<string>? tags)
    {
        return (tags ?? Array.Empty<string>())
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
