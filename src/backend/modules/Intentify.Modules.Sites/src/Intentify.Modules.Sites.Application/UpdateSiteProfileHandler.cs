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
        var tags = (command.Tags ?? Array.Empty<string>())
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var updated = await _sites.UpdateProfileAsync(
            command.TenantId,
            command.SiteId,
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
