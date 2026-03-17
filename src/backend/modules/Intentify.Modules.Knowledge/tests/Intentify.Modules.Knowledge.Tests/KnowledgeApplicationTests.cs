using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Xunit;

namespace Intentify.Modules.Knowledge.Tests;

public sealed class KnowledgeApplicationTests
{
    [Fact]
    public async Task CreateSource_AssignsResolvedBotId()
    {
        var sourceRepo = new RecordingSourceRepository();
        var botId = Guid.NewGuid();
        var handler = new CreateKnowledgeSourceHandler(sourceRepo, new StubBotResolver(botId), new StubSiteRepository());

        var result = await handler.HandleAsync(new CreateKnowledgeSourceCommand(Guid.NewGuid(), Guid.NewGuid(), "Text", "name", null, "content"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(sourceRepo.Inserted);
        Assert.Equal(botId, sourceRepo.Inserted!.BotId);
    }

    [Fact]
    public async Task RetrieveTopChunks_FiltersByBot_WithSiteFallback()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var matchingBotId = Guid.NewGuid();
        var otherBotId = Guid.NewGuid();
        var fallbackSourceId = Guid.NewGuid();
        var matchingSourceId = Guid.NewGuid();
        var otherSourceId = Guid.NewGuid();

        var sourceRepo = new RetrievalSourceRepository([
            new KnowledgeSource { Id = fallbackSourceId, TenantId = tenantId, SiteId = siteId, BotId = Guid.Empty },
            new KnowledgeSource { Id = matchingSourceId, TenantId = tenantId, SiteId = siteId, BotId = matchingBotId },
            new KnowledgeSource { Id = otherSourceId, TenantId = tenantId, SiteId = siteId, BotId = otherBotId }
        ]);

        var chunkRepo = new RetrievalChunkRepository([
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = fallbackSourceId, ChunkIndex = 0, Content = "alpha fallback", CreatedAtUtc = DateTime.UtcNow },
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = matchingSourceId, ChunkIndex = 1, Content = "alpha match", CreatedAtUtc = DateTime.UtcNow },
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = otherSourceId, ChunkIndex = 2, Content = "alpha other", CreatedAtUtc = DateTime.UtcNow }
        ]);

        var handler = new RetrieveTopChunksHandler(chunkRepo, sourceRepo, NullLogger<RetrieveTopChunksHandler>.Instance);
        var results = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "alpha", 5, matchingBotId));

        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, item => item.SourceId == otherSourceId);
    }



    [Fact]
    public async Task RetrieveTopChunks_NormalizesPunctuationInQueryTerms()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var sourceRepo = new RetrievalSourceRepository([
            new KnowledgeSource { Id = sourceId, TenantId = tenantId, SiteId = siteId, BotId = Guid.Empty }
        ]);

        var chunkRepo = new RetrievalChunkRepository([
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = sourceId, ChunkIndex = 0, Content = "Returns are accepted in 30 days", CreatedAtUtc = DateTime.UtcNow }
        ]);

        var handler = new RetrieveTopChunksHandler(chunkRepo, sourceRepo, NullLogger<RetrieveTopChunksHandler>.Instance);
        var results = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "returns?", 5));

        Assert.Single(results);
    }

    [Fact]
    public void RetrieveTopChunks_ScoreChunk_AppliesBonusBlocksOnce()
    {
        var method = typeof(RetrieveTopChunksHandler).GetMethod("ScoreChunk", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        const string content = "# Return Policy\n\nReturn policy details.";
        var score = (int)method!.Invoke(null, new object[] { content, new[] { "return", "policy" }, "return policy" })!;

        Assert.Equal(29, score);
    }

    [Fact]
    public async Task RetrieveTopChunks_UsesOpenSearch_WhenEnabled()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var botId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var sourceRepo = new CountingSourceRepository();
        var chunkRepo = new CountingChunkRepository();
        var openSearchClient = new RecordingOpenSearchKnowledgeClient([
            new OpenSearchChunkDocument(sourceId, chunkId, 2, "open search alpha answer", botId)
        ]);

        var handler = new RetrieveTopChunksHandler(
            chunkRepo,
            sourceRepo,
            NullLogger<RetrieveTopChunksHandler>.Instance,
            new EnabledOpenSearchOptions(),
            openSearchClient);

        var results = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "alpha", 3, botId));

        Assert.Single(results);
        Assert.Equal(chunkId, results.First().ChunkId);
        Assert.Equal(sourceId, results.First().SourceId);
        Assert.Equal(0, sourceRepo.ListSourcesCallCount);
        Assert.Equal(0, chunkRepo.ListBySiteCallCount);

        Assert.NotNull(openSearchClient.LastSearch);
        Assert.Equal(tenantId, openSearchClient.LastSearch!.TenantId);
        Assert.Equal(siteId, openSearchClient.LastSearch.SiteId);
        Assert.Equal(botId, openSearchClient.LastSearch.BotId);
        Assert.Equal("alpha", openSearchClient.LastSearch.Query);
        Assert.Equal(3, openSearchClient.LastSearch.TopK);
    }

    [Fact]
    public async Task TextExtraction_ReturnsInput()
    {
        var extractor = new KnowledgeTextExtractor(new FakeFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var source = new KnowledgeSource { Type = "Text", TextContent = "hello world" };

        var result = await extractor.ExtractAsync(source);

        Assert.True(result.IsSuccess);
        Assert.Equal("hello world", result.Text);
    }

    [Fact]
    public async Task UrlExtraction_StripsHtmlTags()
    {
        var extractor = new KnowledgeTextExtractor(new FakeFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><nav>Cookie Settings Privacy Policy</nav><h1>Title</h1><p>Hello <b>world</b></p><script>window.gtag('x')</script></body></html>")
            }));

        var result = await extractor.ExtractAsync(new KnowledgeSource { Type = "Url", Url = "https://example.local" });

        Assert.True(result.IsSuccess);
        Assert.Contains("# Title", result.Text);
        Assert.Contains("Hello world", result.Text);
        Assert.DoesNotContain("gtag", result.Text!.ToLowerInvariant());
        Assert.DoesNotContain("cookie settings", result.Text.ToLowerInvariant());
    }

    [Fact]
    public void Chunking_SplitBoundary_DoesNotReseedOverflowParagraph()
    {
        var chunker = new KnowledgeChunker();
        var input = "# Services\n\nParagraph one for boundary behavior.\n\nParagraph two must be its own chunk.\n\nParagraph three must follow chunk two.";

        var chunks = chunker.Chunk(input, 70);

        Assert.Equal(
            [
                "# Services\n\nParagraph one for boundary behavior.",
                "# Services\n\nParagraph two must be its own chunk.",
                "# Services\n\nParagraph three must follow chunk two."
            ],
            chunks);
    }

    [Fact]
    public void Chunking_LongHeading_DoesNotDropParagraphPayload()
    {
        var chunker = new KnowledgeChunker();
        var veryLongHeading = "# " + new string('H', 90);
        var firstParagraph = "This payload paragraph must remain intact.";
        var secondParagraph = "Second payload paragraph also remains intact.";
        var input = $"{veryLongHeading}\n\n{firstParagraph}\n\n{secondParagraph}";

        var chunks = chunker.Chunk(input, 60);

        Assert.Equal(2, chunks.Count);
        Assert.Equal($"{veryLongHeading}\n\n{firstParagraph}", chunks[0]);
        Assert.Equal($"{veryLongHeading}\n\n{secondParagraph}", chunks[1]);
    }

    [Fact]
    public async Task RetrieveTopChunks_NormalizesPluralAndTypos()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var sourceRepo = new RetrievalSourceRepository([
            new KnowledgeSource { Id = sourceId, TenantId = tenantId, SiteId = siteId, BotId = Guid.Empty }
        ]);

        var chunkRepo = new RetrievalChunkRepository([
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = sourceId, ChunkIndex = 0, Content = "Return policy: refunds accepted within 30 days.", CreatedAtUtc = DateTime.UtcNow },
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = sourceId, ChunkIndex = 1, Content = "Book a haircut appointment today.", CreatedAtUtc = DateTime.UtcNow }
        ]);

        var handler = new RetrieveTopChunksHandler(chunkRepo, sourceRepo, NullLogger<RetrieveTopChunksHandler>.Instance);

        var pluralResults = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "returns", 2));
        Assert.True(pluralResults.Count > 0);
        Assert.Contains("Return policy", pluralResults.First().Content, StringComparison.OrdinalIgnoreCase);

        var typoResults = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "returrn policy", 2));
        Assert.True(typoResults.Count > 0);
        Assert.Contains("Return policy", typoResults.First().Content, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task RetrieveTopChunks_OneEditTypoStillProducesUsableScore()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var sourceRepo = new RetrievalSourceRepository([
            new KnowledgeSource { Id = sourceId, TenantId = tenantId, SiteId = siteId, BotId = Guid.Empty }
        ]);

        var chunkRepo = new RetrievalChunkRepository([
            new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, SourceId = sourceId, ChunkIndex = 0, Content = "Contact details: email hello@example.com", CreatedAtUtc = DateTime.UtcNow }
        ]);

        var handler = new RetrieveTopChunksHandler(chunkRepo, sourceRepo, NullLogger<RetrieveTopChunksHandler>.Instance);
        var results = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId, siteId, "contct dtails", 2));

        var top = Assert.Single(results);
        Assert.True(top.Score >= 2);
    }


}

internal sealed class EnabledOpenSearchOptions : IOpenSearchOptions
{
    public bool Enabled => true;
}

internal sealed class CountingSourceRepository : IKnowledgeSourceRepository
{
    public int ListSourcesCallCount { get; private set; }

    public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<KnowledgeSource?>(null);

    public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        ListSourcesCallCount++;
        return Task.FromResult<IReadOnlyCollection<KnowledgeSource>>([]);
    }

    public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, int? chunkCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class CountingChunkRepository : IKnowledgeChunkRepository
{
    public int ListBySiteCallCount { get; private set; }

    public Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        ListBySiteCallCount++;
        return Task.FromResult<IReadOnlyCollection<KnowledgeChunk>>([]);
    }

    public Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class RecordingOpenSearchKnowledgeClient : IOpenSearchKnowledgeClient
{
    private readonly IReadOnlyCollection<OpenSearchChunkDocument> _results;

    public RecordingOpenSearchKnowledgeClient(IReadOnlyCollection<OpenSearchChunkDocument> results)
    {
        _results = results;
    }

    public SearchCall? LastSearch { get; private set; }

    public Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task BulkUpsertChunksAsync(Guid tenantId, Guid siteId, Guid? botId, IReadOnlyCollection<OpenSearchChunkDocument> chunkDocs, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyCollection<OpenSearchChunkDocument>> SearchTopChunksAsync(Guid tenantId, Guid siteId, Guid? botId, string query, int topK, CancellationToken cancellationToken = default)
    {
        LastSearch = new SearchCall(tenantId, siteId, botId, query, topK);
        return Task.FromResult(_results);
    }

    public Task DeleteBySourceAsync(Guid tenantId, Guid siteId, Guid sourceId, Guid? botId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    internal sealed record SearchCall(Guid TenantId, Guid SiteId, Guid? BotId, string Query, int TopK);
}

public sealed class IndexingStatusTransitionTests
{
    [Fact]
    public async Task Indexing_WhenAlreadyProcessing_DoesNotReprocess()
    {
        var source = new KnowledgeSource
        {
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            Type = "Text",
            TextContent = "alpha beta",
            Status = IndexStatus.Processing,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IndexedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        var sourceRepo = new InMemorySourceRepository(source);
        var chunkRepo = new InMemoryChunkRepository();
        var handler = new IndexKnowledgeSourceHandler(sourceRepo, chunkRepo, new KnowledgeTextExtractor(new FakeFactory(_ => new HttpResponseMessage(HttpStatusCode.OK))), new KnowledgeChunker(), new StubSiteRepository());

        var result = await handler.HandleAsync(new IndexKnowledgeSourceCommand(source.TenantId, source.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Processing", result.Value!.Status);
        Assert.Equal(0, result.Value.ChunkCount);
        Assert.Equal(0, sourceRepo.UpdateStatusCalls);
        Assert.Empty(chunkRepo.Stored);
    }

    [Fact]
    public async Task Indexing_FailedExtraction_PreservesLastIndexedAt()
    {
        var previousIndexedAt = DateTime.UtcNow.AddHours(-2);
        var source = new KnowledgeSource
        {
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            Type = "Url",
            Url = null,
            Status = IndexStatus.Indexed,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IndexedAtUtc = previousIndexedAt
        };

        var sourceRepo = new InMemorySourceRepository(source);
        var chunkRepo = new InMemoryChunkRepository();
        var handler = new IndexKnowledgeSourceHandler(sourceRepo, chunkRepo, new KnowledgeTextExtractor(new FakeFactory(_ => new HttpResponseMessage(HttpStatusCode.OK))), new KnowledgeChunker(), new StubSiteRepository());

        var result = await handler.HandleAsync(new IndexKnowledgeSourceCommand(source.TenantId, source.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value!.Status);
        Assert.Equal(previousIndexedAt, sourceRepo.Stored.IndexedAtUtc);
        Assert.Equal(IndexStatus.Failed, sourceRepo.Stored.Status);
        Assert.Empty(chunkRepo.Stored);
    }

    [Fact]
    public async Task Indexing_TransitionsQueuedToIndexed()
    {
        var source = new KnowledgeSource
        {
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            Type = "Text",
            TextContent = "alpha beta",
            Status = IndexStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var sourceRepo = new InMemorySourceRepository(source);
        var chunkRepo = new InMemoryChunkRepository();
        var handler = new IndexKnowledgeSourceHandler(sourceRepo, chunkRepo, new KnowledgeTextExtractor(new FakeFactory(_ => new HttpResponseMessage(HttpStatusCode.OK))), new KnowledgeChunker(), new StubSiteRepository());

        var result = await handler.HandleAsync(new IndexKnowledgeSourceCommand(source.TenantId, source.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexStatus.Indexed, sourceRepo.Stored.Status);
        Assert.NotNull(sourceRepo.Stored.IndexedAtUtc);
        Assert.Single(chunkRepo.Stored);
    }

    private sealed class InMemorySourceRepository : IKnowledgeSourceRepository
    {
        public KnowledgeSource Stored { get; }

        public int UpdateStatusCalls { get; private set; }

        public InMemorySourceRepository(KnowledgeSource source)
        {
            Stored = source;
        }

        public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<KnowledgeSource?>(Stored);
        public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<KnowledgeSource>>([Stored]);

        public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, int? chunkCount, CancellationToken cancellationToken = default)
        {
            UpdateStatusCalls++;
            Stored.Status = status;
            Stored.FailureReason = failureReason;
            Stored.IndexedAtUtc = indexedAtUtc;
            return Task.CompletedTask;
        }

        public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class InMemoryChunkRepository : IKnowledgeChunkRepository
    {
        public List<KnowledgeChunk> Stored { get; } = [];

        public Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        {
            Stored.Clear();
            Stored.AddRange(chunks);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<KnowledgeChunk>>(Stored);

        public Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

internal sealed class FakeFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new FakeMessageHandler(_handler));
    }
}

internal sealed class StubBotResolver : IEngageBotResolver
{
    private readonly Guid _botId;

    public StubBotResolver(Guid botId)
    {
        _botId = botId;
    }

    public Task<Guid> GetOrCreateForSiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_botId);
    }
}

internal sealed class RecordingSourceRepository : IKnowledgeSourceRepository
{
    public KnowledgeSource? Inserted { get; private set; }

    public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        Inserted = source;
        return Task.CompletedTask;
    }

    public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<KnowledgeSource?>(null);
    public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<KnowledgeSource>>([]);
    public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, int? chunkCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class RetrievalSourceRepository : IKnowledgeSourceRepository
{
    private readonly IReadOnlyCollection<KnowledgeSource> _sources;

    public RetrievalSourceRepository(IReadOnlyCollection<KnowledgeSource> sources)
    {
        _sources = sources;
    }

    public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(_sources.FirstOrDefault(item => item.Id == sourceId));
    public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(_sources);
    public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, int? chunkCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal sealed class RetrievalChunkRepository : IKnowledgeChunkRepository
{
    private readonly IReadOnlyCollection<KnowledgeChunk> _chunks;

    public RetrievalChunkRepository(IReadOnlyCollection<KnowledgeChunk> chunks)
    {
        _chunks = chunks;
    }

    public Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(_chunks);
    public Task DeleteBySourceAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }

    private sealed class StubSiteRepository : ISiteRepository
    {
        public Task<Site?> GetByTenantAndDomainAsync(Guid tenantId, string domain, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetByTenantAndIdAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<Site?>(new Site { TenantId = tenantId, Id = siteId, Name = "Example", Domain = "example.com", SiteKey = "site-key", WidgetKey = "widget-key" });
        public Task<Site?> GetByWidgetKeyAsync(string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> GetBySiteKeyAsync(string siteKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<IReadOnlyCollection<Site>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyCollection<Site>)Array.Empty<Site>());
        public Task<bool> TenantHasSiteAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task InsertAsync(Site site, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Site?> UpdateProfileAsync(Guid tenantId, Guid siteId, string? name, string domain, string? description, string? category, IReadOnlyCollection<string> tags, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> UpdateAllowedOriginsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<string> allowedOrigins, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> RotateKeysAsync(Guid tenantId, Guid siteId, string siteKey, string widgetKey, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<Site?> UpdateFirstEventReceivedAsync(Guid siteId, DateTime timestampUtc, CancellationToken cancellationToken = default) => Task.FromResult<Site?>(null);
        public Task<bool> DeleteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

}
