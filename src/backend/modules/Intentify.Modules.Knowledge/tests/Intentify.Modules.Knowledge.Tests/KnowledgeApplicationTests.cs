using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Xunit;

namespace Intentify.Modules.Knowledge.Tests;

public sealed class KnowledgeApplicationTests
{
    [Fact]
    public async Task CreateSource_AssignsResolvedBotId()
    {
        var sourceRepo = new RecordingSourceRepository();
        var botId = Guid.NewGuid();
        var handler = new CreateKnowledgeSourceHandler(sourceRepo, new StubBotResolver(botId));

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
                Content = new StringContent("<html><body><h1>Title</h1><p>Hello <b>world</b></p></body></html>")
            }));

        var result = await extractor.ExtractAsync(new KnowledgeSource { Type = "Url", Url = "https://example.local" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Title Hello world", result.Text);
    }

    [Fact]
    public void Chunking_IsDeterministic()
    {
        var chunker = new KnowledgeChunker();
        var input = string.Join("\n\n", Enumerable.Repeat("abcdefghij", 20));

        var chunks = chunker.Chunk(input, 50);

        Assert.Equal(5, chunks.Count);
        Assert.Equal(chunks, chunker.Chunk(input, 50));
    }
}

public sealed class IndexingStatusTransitionTests
{
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
        var handler = new IndexKnowledgeSourceHandler(sourceRepo, chunkRepo, new KnowledgeTextExtractor(new FakeFactory(_ => new HttpResponseMessage(HttpStatusCode.OK))), new KnowledgeChunker());

        var result = await handler.HandleAsync(new IndexKnowledgeSourceCommand(source.TenantId, source.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexStatus.Indexed, sourceRepo.Stored.Status);
        Assert.NotNull(sourceRepo.Stored.IndexedAtUtc);
        Assert.Single(chunkRepo.Stored);
    }

    private sealed class InMemorySourceRepository : IKnowledgeSourceRepository
    {
        public KnowledgeSource Stored { get; }

        public InMemorySourceRepository(KnowledgeSource source)
        {
            Stored = source;
        }

        public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<KnowledgeSource?>(Stored);
        public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<KnowledgeSource>>([Stored]);

        public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default)
        {
            Stored.Status = status;
            Stored.FailureReason = failureReason;
            Stored.IndexedAtUtc = indexedAtUtc;
            return Task.CompletedTask;
        }

        public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
    public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
    public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
}
