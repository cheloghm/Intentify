using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenSearchChunkDocument = Intentify.Modules.Knowledge.Application.OpenSearchChunkDocument;

namespace Intentify.Modules.Knowledge.Infrastructure;

internal sealed class OpenSearchRestClient : IOpenSearchKnowledgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OpenSearchOptions _options;
    private readonly ILogger<OpenSearchRestClient> _logger;

    public OpenSearchRestClient(HttpClient httpClient, OpenSearchOptions options, ILogger<OpenSearchRestClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, $"/{_options.IndexName}");
        ApplyBasicAuthIfConfigured(headRequest);

        using var headResponse = await _httpClient.SendAsync(headRequest, cancellationToken);
        if (headResponse.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        if (headResponse.StatusCode != HttpStatusCode.NotFound)
        {
            _logger.LogError("OpenSearch index existence check failed with status {StatusCode} for index {IndexName}.", (int)headResponse.StatusCode, _options.IndexName);
            headResponse.EnsureSuccessStatusCode();
        }

        var createPayload = new
        {
            mappings = new
            {
                properties = new
                {
                    tenantId = new { type = "keyword" },
                    siteId = new { type = "keyword" },
                    botId = new { type = "keyword" },
                    sourceId = new { type = "keyword" },
                    chunkId = new { type = "keyword" },
                    chunkIndex = new { type = "integer" },
                    content = new { type = "text" }
                }
            }
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Put, $"/{_options.IndexName}")
        {
            Content = JsonContent.Create(createPayload, options: JsonOptions)
        };
        ApplyBasicAuthIfConfigured(createRequest);

        using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            _logger.LogError("OpenSearch index creation failed with status {StatusCode} for index {IndexName}.", (int)createResponse.StatusCode, _options.IndexName);
            createResponse.EnsureSuccessStatusCode();
        }
    }

    public async Task BulkUpsertChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        IReadOnlyCollection<OpenSearchChunkDocument> chunkDocs,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || chunkDocs.Count == 0)
        {
            return;
        }

        await EnsureIndexExistsAsync(cancellationToken);

        var payload = BuildBulkPayload(tenantId, siteId, botId, chunkDocs);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/_bulk")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/x-ndjson")
        };
        ApplyBasicAuthIfConfigured(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenSearch bulk upsert failed for tenant {TenantId}, site {SiteId}, bot {BotId} with {Count} chunks and status {StatusCode}.",
                tenantId,
                siteId,
                botId,
                chunkDocs.Count,
                (int)response.StatusCode);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "OpenSearch bulk upsert completed for tenant {TenantId}, site {SiteId}, bot {BotId} with {Count} chunks.",
            tenantId,
            siteId,
            botId,
            chunkDocs.Count);
    }

    public async Task<IReadOnlyCollection<OpenSearchChunkDocument>> SearchTopChunksAsync(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        await EnsureIndexExistsAsync(cancellationToken);

        var mustClauses = new List<object>
        {
            new { term = new Dictionary<string, string> { ["tenantId"] = tenantId.ToString("D") } },
            new { term = new Dictionary<string, string> { ["siteId"] = siteId.ToString("D") } }
        };

        if (botId.HasValue)
        {
            mustClauses.Add(new
            {
                @bool = new
                {
                    should = new object[]
                    {
                        new { term = new Dictionary<string, string> { ["botId"] = botId.Value.ToString("D") } },
                        new { @bool = new { must_not = new { exists = new { field = "botId" } } } }
                    },
                    minimum_should_match = 1
                }
            });
        }

        var searchPayload = new
        {
            size = topK,
            query = new
            {
                @bool = new
                {
                    must = mustClauses,
                    should = new object[]
                    {
                        new { match = new Dictionary<string, string> { ["content"] = query } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{_options.IndexName}/_search")
        {
            Content = JsonContent.Create(searchPayload, options: JsonOptions)
        };
        ApplyBasicAuthIfConfigured(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenSearch search failed for tenant {TenantId}, site {SiteId}, bot {BotId}, topK {TopK} with status {StatusCode}.",
                tenantId,
                siteId,
                botId,
                topK,
                (int)response.StatusCode);

            response.EnsureSuccessStatusCode();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("hits", out var hitsElement) ||
            !hitsElement.TryGetProperty("hits", out var innerHitsElement) ||
            innerHitsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<OpenSearchChunkDocument>();
        foreach (var hit in innerHitsElement.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var source))
            {
                continue;
            }

            var chunkId = source.TryGetProperty("chunkId", out var chunkIdElement) && Guid.TryParse(chunkIdElement.GetString(), out var parsedChunkId)
                ? parsedChunkId
                : Guid.Empty;

            var sourceId = source.TryGetProperty("sourceId", out var sourceIdElement) && Guid.TryParse(sourceIdElement.GetString(), out var parsedSourceId)
                ? parsedSourceId
                : Guid.Empty;

            if (chunkId == Guid.Empty || sourceId == Guid.Empty)
            {
                continue;
            }

            var chunkIndex = source.TryGetProperty("chunkIndex", out var chunkIndexElement) && chunkIndexElement.TryGetInt32(out var parsedChunkIndex)
                ? parsedChunkIndex
                : 0;

            var content = source.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;

            results.Add(new OpenSearchChunkDocument(sourceId, chunkId, chunkIndex, content));
        }

        _logger.LogInformation(
            "OpenSearch search completed for tenant {TenantId}, site {SiteId}, bot {BotId} with {ResultCount} results (topK {TopK}).",
            tenantId,
            siteId,
            botId,
            results.Count,
            topK);

        return results;
    }

    public async Task DeleteBySourceAsync(Guid tenantId, Guid siteId, Guid sourceId, Guid? botId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        await EnsureIndexExistsAsync(cancellationToken);

        var mustClauses = new List<object>
        {
            new { term = new Dictionary<string, string> { ["tenantId"] = tenantId.ToString("D") } },
            new { term = new Dictionary<string, string> { ["siteId"] = siteId.ToString("D") } },
            new { term = new Dictionary<string, string> { ["sourceId"] = sourceId.ToString("D") } }
        };

        if (botId.HasValue)
        {
            mustClauses.Add(new { term = new Dictionary<string, string> { ["botId"] = botId.Value.ToString("D") } });
        }

        var payload = new
        {
            query = new
            {
                @bool = new
                {
                    must = mustClauses
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{_options.IndexName}/_delete_by_query")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyBasicAuthIfConfigured(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string BuildBulkPayload(
        Guid tenantId,
        Guid siteId,
        Guid? botId,
        IReadOnlyCollection<OpenSearchChunkDocument> chunkDocs)
    {
        var builder = new StringBuilder();

        foreach (var doc in chunkDocs)
        {
            var metadata = new
            {
                index = new
                {
                    _index = _options.IndexName,
                    _id = doc.ChunkId.ToString("D")
                }
            };

            var body = new
            {
                tenantId = tenantId.ToString("D"),
                siteId = siteId.ToString("D"),
                botId = botId?.ToString("D"),
                sourceId = doc.SourceId.ToString("D"),
                chunkId = doc.ChunkId.ToString("D"),
                chunkIndex = doc.ChunkIndex,
                content = doc.Content
            };

            builder.AppendLine(JsonSerializer.Serialize(metadata, JsonOptions));
            builder.AppendLine(JsonSerializer.Serialize(body, JsonOptions));
        }

        return builder.ToString();
    }

    private void ApplyBasicAuthIfConfigured(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return;
        }

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
