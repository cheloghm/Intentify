using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Knowledge.Application;

public sealed class UploadPdfHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;

    public UploadPdfHandler(IKnowledgeSourceRepository sourceRepository)
    {
        _sourceRepository = sourceRepository;
    }

    public async Task<OperationResult<CreateKnowledgeSourceResult>> HandleAsync(UploadPdfCommand command, CancellationToken cancellationToken = default)
    {
        var source = await _sourceRepository.GetSourceByIdAsync(command.TenantId, command.SourceId, cancellationToken);
        if (source is null)
        {
            return OperationResult<CreateKnowledgeSourceResult>.NotFound();
        }

        if (!source.Type.Equals("Pdf", StringComparison.OrdinalIgnoreCase))
        {
            var errors = new ValidationErrors();
            errors.Add("sourceId", "Source is not Pdf type.");
            return OperationResult<CreateKnowledgeSourceResult>.ValidationFailed(errors);
        }

        await _sourceRepository.ReplaceSourceContentAsync(command.TenantId, command.SourceId, command.Bytes, IndexStatus.Queued, DateTime.UtcNow, cancellationToken);
        return OperationResult<CreateKnowledgeSourceResult>.Success(new CreateKnowledgeSourceResult(command.SourceId, IndexStatus.Queued.ToString()));
    }
}
