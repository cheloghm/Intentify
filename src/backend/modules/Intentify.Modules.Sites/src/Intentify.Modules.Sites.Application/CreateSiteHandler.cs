using Intentify.Modules.Sites.Domain;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Sites.Application;

public sealed class CreateSiteHandler
{
    private readonly ISiteRepository _sites;
    private readonly IKeyGenerator _keyGenerator;
    private readonly ISiteKnowledgeSeeder _knowledgeSeeder;

    public CreateSiteHandler(ISiteRepository sites, IKeyGenerator keyGenerator, ISiteKnowledgeSeeder knowledgeSeeder)
    {
        _sites = sites;
        _keyGenerator = keyGenerator;
        _knowledgeSeeder = knowledgeSeeder;
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

        // Plan-based site limit is enforced by the API layer (SitesEndpoints) using PlanLimits.
        // The handler trusts that the caller has already performed the check.

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
            SnippetId = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sites.InsertAsync(site, cancellationToken);

        // Auto-populate AllowedOrigins from the domain
        var allowedOrigins = BuildAllowedOrigins(normalizedDomain);
        var updatedSite = await _sites.UpdateAllowedOriginsAsync(command.TenantId, site.Id, allowedOrigins, cancellationToken);

        // Seed default knowledge sources (URL crawler + quick facts template)
        await _knowledgeSeeder.SeedDefaultSourcesAsync(command.TenantId, site.Id, normalizedDomain, cancellationToken);

        return OperationResult<Site>.Success(updatedSite ?? site);
    }

    private static List<string> BuildAllowedOrigins(string normalizedDomain)
    {
        var origins = new List<string> { $"https://{normalizedDomain}" };
        if (!normalizedDomain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            origins.Add($"https://www.{normalizedDomain}");
        }
        return origins;
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
