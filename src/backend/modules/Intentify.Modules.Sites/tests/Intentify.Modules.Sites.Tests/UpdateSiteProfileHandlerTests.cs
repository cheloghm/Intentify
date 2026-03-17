using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;
using Xunit;

namespace Intentify.Modules.Sites.Tests;

public sealed class UpdateSiteProfileHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenDomainBelongsToAnotherSite()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var repository = new InMemorySiteRepository(tenantId, siteId, "current.local");
        repository.DuplicateSite = new Site { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Other", Domain = "localhost" };

        var handler = new UpdateSiteProfileHandler(repository);
        var result = await handler.HandleAsync(new UpdateSiteProfileCommand(tenantId, siteId, "Name", "LOCALHOST", null, null, null));

        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task HandleAsync_AllowsSameDomain_WithoutConflict()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var repository = new InMemorySiteRepository(tenantId, siteId, "localhost");
        repository.DuplicateSite = new Site { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Other", Domain = "localhost" };

        var handler = new UpdateSiteProfileHandler(repository);
        var result = await handler.HandleAsync(new UpdateSiteProfileCommand(tenantId, siteId, "Updated", "LOCALHOST", null, null, null));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("localhost", result.Value!.Domain);
        Assert.Equal("Updated", result.Value.Name);
    }

    private sealed class InMemorySiteRepository : ISiteRepository
    {
        private Site _site;

        public InMemorySiteRepository(Guid tenantId, Guid siteId, string domain)
        {
            _site = new Site { Id = siteId, TenantId = tenantId, Name = "Existing", Domain = domain, SiteKey = "site", WidgetKey = "widget" };
        }

        public Site? DuplicateSite { get; set; }

        public Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default)
            => Task.FromResult(DuplicateSite);

        public Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<Site?>(_site.TenantId == tenantId && _site.Id == siteId ? _site : null);

        public Task<Site?> GetByWidgetKeyAsync(string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<IReadOnlyCollection<Site>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyCollection<Site>)[]);
        public Task<bool> TenantHasSiteAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task InsertAsync(Site site, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Site?> UpdateProfileAsync(Guid tenantId, Guid siteId, string? name, string domain, string? description, string? category, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default)
        {
            _site = new Site
            {
                Id = _site.Id,
                TenantId = _site.TenantId,
                Name = name ?? domain,
                Domain = domain,
                Description = description,
                Category = category,
                Tags = tags.ToList(),
                AllowedOrigins = _site.AllowedOrigins,
                SiteKey = _site.SiteKey,
                WidgetKey = _site.WidgetKey,
                CreatedAtUtc = _site.CreatedAtUtc,
                UpdatedAtUtc = DateTime.UtcNow,
                FirstEventReceivedAtUtc = _site.FirstEventReceivedAtUtc
            };

            return Task.FromResult<Site?>(_site);
        }

        public Task<Site?> UpdateAllowedOriginsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<string> allowedOrigins, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> RotateKeysAsync(Guid tenantId, Guid siteId, string siteKey, string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<bool> DeleteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
