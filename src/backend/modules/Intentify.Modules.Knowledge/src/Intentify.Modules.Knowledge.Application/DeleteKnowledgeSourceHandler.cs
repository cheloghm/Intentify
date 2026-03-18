using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intentify.Modules.Knowledge.Application;

public sealed record DeleteKnowledgeSourceCommand(Guid TenantId, Guid SourceId);

public sealed record DeleteKnowledgeSourceResult(Guid SourceId);

public sealed class DeleteKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IOpenSearchOptions? _openSearchOptions;
    private readonly IOpenSearchKnowledgeClient? _openSearchClient;
    private readonly ILogger<DeleteKnowledgeSourceHandler> _logger;

    public DeleteKnowledgeSourceHandler(
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository,
        IOpenSearchOptions? openSearchOptions = null,
        IOpenSearchKnowledgeClient? openSearchClient = null,
        ILogger<DeleteKnowledgeSourceHandler>? logger = null)
    {
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
        _openSearchOptions = openSearchOptions;
        _openSearchClient = openSearchClient;
        _logger = logger ?? NullLogger<DeleteKnowledgeSourceHandler>.Instance;
    }

    public async Task<OperationResult<DeleteKnowledgeSourceResult>> HandleAsync(
        DeleteKnowledgeSourceCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(command.TenantId, command.SourceId, cancellationToken);
        if (source is null)
        {
            return OperationResult<DeleteKnowledgeSourceResult>.NotFound();
        }

        await _chunkRepository.DeleteBySourceAsync(command.TenantId, command.SourceId, cancellationToken);

        if (_openSearchOptions?.Enabled == true && _openSearchClient is not null)
        {
            _logger.LogInformation(
                "OpenSearch delete path is enabled and wired for tenant {TenantId}, site {SiteId}, source {SourceId}, bot {BotId}.",
                source.TenantId,
                source.SiteId,
                source.Id,
                source.BotId);

            await _openSearchClient.DeleteBySourceAsync(
                source.TenantId,
                source.SiteId,
                source.Id,
                source.BotId,
                cancellationToken);
        }
        else if (_openSearchOptions?.Enabled == true && _openSearchClient is null)
        {
            _logger.LogWarning(
                "OpenSearch delete is enabled but OpenSearch client dependency is unavailable for tenant {TenantId}, site {SiteId}, source {SourceId}, bot {BotId}. Proceeding with Mongo delete only.",
                source.TenantId,
                source.SiteId,
                source.Id,
                source.BotId);
        }

        var deleted = await _sourceRepository.DeleteSourceAsync(command.TenantId, command.SourceId, cancellationToken);

        return deleted
            ? OperationResult<DeleteKnowledgeSourceResult>.Success(new DeleteKnowledgeSourceResult(command.SourceId))
            : OperationResult<DeleteKnowledgeSourceResult>.NotFound();
    }
}
