using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Validation;
using Xunit;

namespace Intentify.Modules.Sites.Tests;

public sealed class ApiKeyHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _siteId   = Guid.NewGuid();

    // ── GenerateApiKey ────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateApiKey_Returns_ItfyPrefixedSecret()
    {
        var (gen, _, _) = BuildHandlers();
        var result = await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, _siteId, "CI Key"));
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.StartsWith("itfy_", result.Value!.RawSecret);
    }

    [Fact]
    public async Task GenerateApiKey_StoresHashNotRaw()
    {
        var (gen, list, _) = BuildHandlers();
        var genResult = await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, _siteId, "CI Key"));
        var rawSecret = genResult.Value!.RawSecret;

        var listResult = await list.HandleAsync(new ListApiKeysCommand(_tenantId, _siteId));
        Assert.DoesNotContain(listResult.Value!, k => k.Hint == rawSecret);
    }

    [Fact]
    public async Task GenerateApiKey_RequiresLabel()
    {
        var (gen, _, _) = BuildHandlers();
        var result = await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, _siteId, ""));
        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task GenerateApiKey_SiteNotFound()
    {
        var (gen, _, _) = BuildHandlers();
        var result = await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, Guid.NewGuid(), "Key"));
        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    // ── ListApiKeys ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListApiKeys_Empty_WhenNoKeys()
    {
        var (_, list, _) = BuildHandlers();
        var result = await list.HandleAsync(new ListApiKeysCommand(_tenantId, _siteId));
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListApiKeys_ShowsActiveKey_AfterGenerate()
    {
        var (gen, list, _) = BuildHandlers();
        await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, _siteId, "Prod Key"));

        var result = await list.HandleAsync(new ListApiKeysCommand(_tenantId, _siteId));
        Assert.Equal(1, result.Value!.Count);
        Assert.True(result.Value[0].IsActive);
        Assert.Equal("Prod Key", result.Value[0].Label);
    }

    // ── RevokeApiKey ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeApiKey_SetsInactive()
    {
        var (gen, list, revoke) = BuildHandlers();
        var genResult = await gen.HandleAsync(new GenerateApiKeyCommand(_tenantId, _siteId, "ToRevoke"));
        var keyId = genResult.Value!.KeyId;

        await revoke.HandleAsync(new RevokeApiKeyCommand(_tenantId, _siteId, keyId));

        var listResult = await list.HandleAsync(new ListApiKeysCommand(_tenantId, _siteId));
        Assert.False(listResult.Value![0].IsActive);
    }

    [Fact]
    public async Task RevokeApiKey_UnknownKey_ReturnsValidationFailed()
    {
        var (_, _, revoke) = BuildHandlers();
        var result = await revoke.HandleAsync(new RevokeApiKeyCommand(_tenantId, _siteId, "nonexistent-key-id"));
        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task RevokeApiKey_SiteNotFound_ReturnsNotFound()
    {
        var (_, _, revoke) = BuildHandlers();
        var result = await revoke.HandleAsync(new RevokeApiKeyCommand(_tenantId, Guid.NewGuid(), "any-key-id"));
        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private (GenerateApiKeyHandler, ListApiKeysHandler, RevokeApiKeyHandler) BuildHandlers()
    {
        var repo = new InMemorySiteRepository(_tenantId, _siteId);
        var gen  = new GenerateApiKeyHandler(repo, new FakeKeyGenerator());
        var list = new ListApiKeysHandler(repo);
        var revoke = new RevokeApiKeyHandler(repo);
        return (gen, list, revoke);
    }

    private sealed class FakeKeyGenerator : IKeyGenerator
    {
        public string GenerateKey(KeyPurpose purpose) => Guid.NewGuid().ToString("N");
    }

    private sealed class InMemorySiteRepository : ISiteRepository
    {
        private readonly Site _site;

        public InMemorySiteRepository(Guid tenantId, Guid siteId)
        {
            _site = new Site
            {
                Id = siteId,
                TenantId = tenantId,
                Name = "Test Site",
                Domain = "test.local",
                SiteKey = "sk",
                WidgetKey = "wk",
            };
        }

        public Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult(_site.TenantId == tenantId && _site.Id == siteId ? _site : (Site?)null);

        public Task AddApiKeyAsync(Guid tenantId, Guid siteId, SiteApiKey apiKey, CancellationToken cancellationToken = default)
        {
            if (_site.TenantId == tenantId && _site.Id == siteId)
                _site.ApiKeys.Add(apiKey);
            return Task.CompletedTask;
        }

        public Task RevokeApiKeyAsync(Guid tenantId, Guid siteId, string keyId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
        {
            if (_site.TenantId == tenantId && _site.Id == siteId)
            {
                var key = _site.ApiKeys.FirstOrDefault(k => k.KeyId == keyId);
                if (key is not null) key.RevokedAtUtc = revokedAtUtc;
            }
            return Task.CompletedTask;
        }

        public Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetByWidgetKeyAsync(string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<IReadOnlyCollection<Site>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyCollection<Site>)[]);
        public Task<bool> TenantHasSiteAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task InsertAsync(Site site, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Site?> UpdateProfileAsync(Guid tenantId, Guid siteId, string? name, string domain, string? description, string? category, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> UpdateAllowedOriginsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<string> allowedOrigins, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> RotateKeysAsync(Guid tenantId, Guid siteId, string siteKey, string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<bool> DeleteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
