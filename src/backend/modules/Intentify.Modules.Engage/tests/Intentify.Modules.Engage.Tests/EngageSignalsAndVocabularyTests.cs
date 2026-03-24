using Intentify.Modules.Engage.Application;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageSignalsAndVocabularyTests
{
    [Fact]
    public void SupportSignalMatcher_ExplicitSupportIssue_IsDetected()
    {
        var matcher = new EngageSupportSignalMatcher(new EngageInputInterpreter());

        var result = matcher.NeedsHumanHelp("I need support, payment failed and I need help.");

        Assert.True(result);
    }

    [Fact]
    public void CommercialSignalMatcher_QuoteIntent_IsDetected()
    {
        var matcher = new EngageCommercialSignalMatcher();

        var result = matcher.IsExplicitCommercialContactRequest("Can you call me with a quote?");

        Assert.True(result);
    }

    [Fact]
    public async Task TenantVocabularyResolver_UsesSiteAndKnowledgeTerms_WithBotScoping()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var botId = Guid.NewGuid();
        var otherBotId = Guid.NewGuid();
        var allowedSourceId = Guid.NewGuid();
        var otherSourceId = Guid.NewGuid();

        var siteRepo = new StubSiteRepository(new Site
        {
            Id = siteId,
            TenantId = tenantId,
            Name = "Acme Plumbing",
            Domain = "acmeplumbing.example",
            Description = "Emergency plumbing services",
            Category = "Home Services",
            Tags = ["plumbing", "repair"]
        });

        var sourceRepo = new StubKnowledgeSourceRepository([
            new KnowledgeSource
            {
                Id = allowedSourceId,
                TenantId = tenantId,
                SiteId = siteId,
                BotId = botId,
                Type = "Text",
                Name = "Service Guide",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new KnowledgeSource
            {
                Id = otherSourceId,
                TenantId = tenantId,
                SiteId = siteId,
                BotId = otherBotId,
                Type = "Text",
                Name = "Other Bot Guide",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        ]);

        var chunkRepo = new StubKnowledgeChunkRepository([
            new KnowledgeChunk
            {
                TenantId = tenantId,
                SiteId = siteId,
                SourceId = allowedSourceId,
                ChunkIndex = 0,
                Content = "Drain cleaning and leak repair for homes.",
                CreatedAtUtc = DateTime.UtcNow
            },
            new KnowledgeChunk
            {
                TenantId = tenantId,
                SiteId = siteId,
                SourceId = otherSourceId,
                ChunkIndex = 1,
                Content = "Unrelated bot specific term xyzabc.",
                CreatedAtUtc = DateTime.UtcNow
            }
        ]);

        var resolver = new TenantVocabularyResolver(siteRepo, sourceRepo, chunkRepo);

        var terms = await resolver.ResolveAsync(tenantId, siteId, botId, CancellationToken.None);

        Assert.Contains("plumbing", terms);
        Assert.Contains("emergency", terms);
        Assert.Contains("drain", terms);
        Assert.DoesNotContain("xyzabc", terms);
    }

    private sealed class StubSiteRepository(Site? site) : ISiteRepository
    {
        public Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(site?.TenantId == tenantId && site.Id == siteId ? site : null);
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

    private sealed class StubKnowledgeSourceRepository(IReadOnlyCollection<KnowledgeSource> sources) : IKnowledgeSourceRepository
    {
        public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(sources.FirstOrDefault(item => item.TenantId == tenantId && item.Id == sourceId));

        public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<KnowledgeSource>)sources.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());

        public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, int? chunkCount, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class StubKnowledgeChunkRepository(IReadOnlyCollection<KnowledgeChunk> chunks) : IKnowledgeChunkRepository
    {
        public Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<KnowledgeChunk>)chunks.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());

        public Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
