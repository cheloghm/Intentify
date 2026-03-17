using Intentify.Shared.Validation;

namespace Intentify.Modules.Knowledge.Application;

public sealed record DeleteKnowledgeSourceCommand(Guid TenantId, Guid SourceId);

public sealed class DeleteKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IKnowledgeChunkRepository _chunkRepository;
    private readonly IOpenSearchOptions? _openSearchOptions;
    private readonly IOpenSearchKnowledgeClient? _openSearchClient;

    public DeleteKnowledgeSourceHandler(
        IKnowledgeSourceRepository sourceRepository,
        IKnowledgeChunkRepository chunkRepository,
        IOpenSearchOptions? openSearchOptions = null,
        IOpenSearchKnowledgeClient? openSearchClient = null)
    {
        _sourceRepository = sourceRepository;
        _chunkRepository = chunkRepository;
        _openSearchOptions = openSearchOptions;
        _openSearchClient = openSearchClient;
    }

    public async Task<OperationResult> HandleAsync(DeleteKnowledgeSourceCommand command, CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(command.TenantId, command.SourceId, cancellationToken);
        if (source is null)
        {
            return OperationResult.NotFound();
        }

        await _chunkRepository.DeleteBySourceAsync(command.TenantId, command.SourceId, cancellationToken);

        if (_openSearchOptions?.Enabled == true && _openSearchClient is not null)
        {
            await _openSearchClient.DeleteBySourceAsync(source.TenantId, source.SiteId, source.Id, source.BotId, cancellationToken);
        }

        var deleted = await _sourceRepository.DeleteSourceAsync(command.TenantId, command.SourceId, cancellationToken);
        return deleted ? OperationResult.Success() : OperationResult.NotFound();
    }
}
