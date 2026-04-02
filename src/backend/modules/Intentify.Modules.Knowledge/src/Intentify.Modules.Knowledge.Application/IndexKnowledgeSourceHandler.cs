using System.Text;
using System.Text.Json;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.AI;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intentify.Modules.Knowledge.Application;

public sealed class IndexKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IKnowledgeQuickFactsRepository _quickFactsRepository;
    private readonly IKnowledgeTextExtractor _extractor;
    private readonly IKnowledgeChunker _chunker;
    private readonly IOpenSearchOptions? _openSearchOptions;
    private readonly IOpenSearchKnowledgeClient? _openSearchClient;
    private readonly IChatCompletionClient? _chatCompletionClient;
    private readonly ILogger<IndexKnowledgeSourceHandler> _logger;
    private readonly ISiteRepository _siteRepository;

    public IndexKnowledgeSourceHandler(
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository,
        IKnowledgeQuickFactsRepository quickFactsRepository,
        IKnowledgeTextExtractor extractor,
        IKnowledgeChunker chunker,
        ISiteRepository siteRepository,
        IOpenSearchOptions? openSearchOptions = null,
        IOpenSearchKnowledgeClient? openSearchClient = null,
        IChatCompletionClient? chatCompletionClient = null,
        ILogger<IndexKnowledgeSourceHandler>? logger = null)
    {
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
        _quickFactsRepository = quickFactsRepository;
        _extractor = extractor;
        _chunker = chunker;
        _openSearchOptions = openSearchOptions;
        _openSearchClient = openSearchClient;
        _chatCompletionClient = chatCompletionClient;
        _logger = logger ?? NullLogger<IndexKnowledgeSourceHandler>.Instance;
        _siteRepository = siteRepository;
    }

    public async Task<OperationResult<IndexKnowledgeSourceResult>> HandleAsync(IndexKnowledgeSourceCommand command, CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(command.TenantId, command.SourceId, cancellationToken);
        if (source is null)
        {
            return OperationResult<IndexKnowledgeSourceResult>.NotFound();
        }

        if (source.Status == IndexStatus.Processing)
        {
            return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Processing.ToString(), 0, null));
        }

        var site = await _siteRepository.GetByTenantAndIdAsync(command.TenantId, source.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<IndexKnowledgeSourceResult>.NotFound();
        }

        await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Processing, null, null, null, cancellationToken);

        var extracted = await _extractor.ExtractAsync(source, cancellationToken);
        if (!extracted.IsSuccess)
        {
            await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Failed, extracted.FailureReason, source.IndexedAtUtc, 0, cancellationToken);
            return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Failed.ToString(), 0, extracted.FailureReason));
        }

        var chunks = _chunker.Chunk(extracted.Text ?? string.Empty)
            .Select((content, index) => new KnowledgeChunk
            {
                TenantId = source.TenantId,
                SiteId = source.SiteId,
                SourceId = source.Id,
                ChunkIndex = index,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow
            })
            .ToArray();

        await _chunkRepository.UpsertChunksAsync(command.TenantId, source.Id, chunks, cancellationToken);
        string? openSearchSyncFailureReason = null;

        if (_openSearchOptions?.Enabled == true && _openSearchClient is not null)
        {
            _logger.LogInformation(
                "OpenSearch indexing path is enabled and wired for tenant {TenantId}, site {SiteId}, source {SourceId}.",
                source.TenantId,
                source.SiteId,
                source.Id);

            try
            {
                var openSearchDocs = chunks
                    .Select(chunk => new OpenSearchChunkDocument(
                        chunk.SourceId,
                        chunk.Id,
                        chunk.ChunkIndex,
                        chunk.Content,
                        source.BotId))
                    .ToArray();

                await _openSearchClient.EnsureIndexExistsAsync(cancellationToken);
                await _openSearchClient.DeleteBySourceAsync(source.TenantId, source.SiteId, source.Id, source.BotId, cancellationToken);
                await _openSearchClient.BulkUpsertChunksAsync(source.TenantId, source.SiteId, source.BotId, openSearchDocs, cancellationToken);
            }
            catch (Exception exception)
            {
                openSearchSyncFailureReason = "OpenSearchSyncFailed";
                _logger.LogWarning(
                    exception,
                    "OpenSearch indexing failed for tenant {TenantId}, site {SiteId}, source {SourceId}. {ExceptionType}: {ExceptionMessage}",
                    source.TenantId,
                    source.SiteId,
                    source.Id,
                    exception.GetType().Name,
                    exception.Message);
            }
        }
        else if (_openSearchOptions?.Enabled == true && _openSearchClient is null)
        {
            _logger.LogWarning(
                "OpenSearch indexing is enabled but OpenSearch client dependency is unavailable for tenant {TenantId}, site {SiteId}, source {SourceId}. Mongo chunk persistence will continue without OpenSearch sync.",
                source.TenantId,
                source.SiteId,
                source.Id);
        }

        // Second AI pass — extract structured quick facts from all chunks
        if (_chatCompletionClient is not null && chunks.Length > 0)
        {
            await ExtractAndStoreQuickFactsAsync(source, chunks, cancellationToken);
        }

        var indexedAt = DateTime.UtcNow;
        await _sourceRepository.UpdateStatusAsync(command.TenantId, source.Id, IndexStatus.Indexed, openSearchSyncFailureReason, indexedAt, chunks.Length, cancellationToken);

        return OperationResult<IndexKnowledgeSourceResult>.Success(new IndexKnowledgeSourceResult(IndexStatus.Indexed.ToString(), chunks.Length, null));
    }

    private async Task ExtractAndStoreQuickFactsAsync(
        KnowledgeSource source,
        KnowledgeChunk[] chunks,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = BuildQuickFactsPrompt(source, chunks);
            var completion = await _chatCompletionClient!.CompleteAsync(string.Empty, prompt, cancellationToken);

            if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
            {
                _logger.LogWarning(
                    "Quick facts extraction produced no output for source {SourceId}.",
                    source.Id);
                return;
            }

            var facts = ParseQuickFacts(completion.Value, source.TenantId, source.SiteId, source.Id);
            if (facts is null)
            {
                _logger.LogWarning(
                    "Quick facts extraction output could not be parsed for source {SourceId}.",
                    source.Id);
                return;
            }

            await _quickFactsRepository.UpsertAsync(facts, cancellationToken);

            _logger.LogInformation(
                "Quick facts extracted and stored for tenant {TenantId}, source {SourceId}.",
                source.TenantId,
                source.Id);
        }
        catch (Exception exception)
        {
            // Quick facts extraction is a best-effort enhancement — never block indexing
            _logger.LogWarning(
                exception,
                "Quick facts extraction failed for source {SourceId}. {ExceptionType}: {ExceptionMessage}",
                source.Id,
                exception.GetType().Name,
                exception.Message);
        }
    }

    private static string BuildQuickFactsPrompt(KnowledgeSource source, KnowledgeChunk[] chunks)
    {
        const int MaxContextChars = 8000;

        var sb = new StringBuilder();
        sb.AppendLine("You are a knowledge extraction assistant.");
        sb.AppendLine("Read the following content from a business knowledge base and extract structured quick facts.");
        sb.AppendLine("Output ONLY valid JSON. No markdown, no prose.");
        sb.AppendLine("Only populate fields where the information is clearly present in the content.");
        sb.AppendLine("Use null for fields where no information exists.");
        sb.AppendLine();
        sb.AppendLine("Output schema:");
        sb.AppendLine("""
            {
              "servicesOffered": "concise list of services/products offered, or null",
              "pricingSignals": "any pricing, cost, or rate information mentioned, or null",
              "locationCoverage": "location, coverage area, or service region, or null",
              "hoursAvailability": "operating hours, response times, or availability, or null",
              "teamCredentials": "team size, qualifications, certifications, or experience, or null",
              "faqsText": "key Q&A pairs in 'Q: ... A: ...' format, one per line, or null",
              "uniqueSellingPoints": "what makes this business stand out, or null"
            }
            """);
        sb.AppendLine();
        sb.AppendLine($"Source: {source.Name ?? source.Url ?? source.Id.ToString("D")}");
        sb.AppendLine();
        sb.AppendLine("Content:");

        var totalChars = 0;
        foreach (var chunk in chunks.OrderBy(c => c.ChunkIndex))
        {
            if (totalChars + chunk.Content.Length > MaxContextChars)
                break;

            sb.AppendLine(chunk.Content);
            sb.AppendLine();
            totalChars += chunk.Content.Length;
        }

        return sb.ToString();
    }

    private static KnowledgeQuickFacts? ParseQuickFacts(string rawOutput, Guid tenantId, Guid siteId, Guid sourceId)
    {
        var trimmed = rawOutput.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        var json = trimmed[start..(end + 1)];

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new KnowledgeQuickFacts
            {
                TenantId          = tenantId,
                SiteId            = siteId,
                SourceId          = sourceId,
                ExtractedAtUtc    = DateTime.UtcNow,
                ServicesOffered   = ReadString(root, "servicesOffered"),
                PricingSignals    = ReadString(root, "pricingSignals"),
                LocationCoverage  = ReadString(root, "locationCoverage"),
                HoursAvailability = ReadString(root, "hoursAvailability"),
                TeamCredentials   = ReadString(root, "teamCredentials"),
                FaqsText          = ReadString(root, "faqsText"),
                UniqueSellingPoints = ReadString(root, "uniqueSellingPoints")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public interface IOpenSearchOptions
{
    bool Enabled { get; }
}

public interface IOpenSearchKnowledgeClient
{
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default);

    Task BulkUpsertChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        IReadOnlyCollection<OpenSearchChunkDocument> chunkDocs,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OpenSearchChunkDocument>> SearchTopChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        string query,
        int topK,
        CancellationToken cancellationToken = default);

    Task DeleteBySourceAsync(
        Guid tenantId,
        Guid siteId,
        Guid sourceId,
        Guid? botId,
        CancellationToken cancellationToken = default);
}

public sealed record OpenSearchChunkDocument(
    Guid SourceId,
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    Guid BotId);
