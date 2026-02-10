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

        var now = DateTime.UtcNow;
        var site = new Site
        {
            TenantId = command.TenantId,
            Domain = normalizedDomain,
            AllowedOrigins = [],
            SiteKey = _keyGenerator.GenerateKey(KeyPurpose.SiteKey),
            WidgetKey = _keyGenerator.GenerateKey(KeyPurpose.WidgetKey),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sites.InsertAsync(site, cancellationToken);

        return OperationResult<Site>.Success(site);
    }
}
