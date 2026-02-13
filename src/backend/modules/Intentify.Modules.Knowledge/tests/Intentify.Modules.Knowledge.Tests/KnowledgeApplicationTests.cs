using System.Net;
using System.Net.Http;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Xunit;

namespace Intentify.Modules.Knowledge.Tests;

public sealed class KnowledgeApplicationTests
{
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
