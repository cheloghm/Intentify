using Intentify.Modules.Sites.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Knowledge.Application;

public sealed class CreateKnowledgeSourceHandler
{
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly IEngageBotResolver _botResolver;
    private readonly ISiteRepository _siteRepository;

    public CreateKnowledgeSourceHandler(IKnowledgeSourceRepository sourceRepository, IEngageBotResolver botResolver, ISiteRepository siteRepository)
    {
        _sourceRepository = sourceRepository;
        _botResolver = botResolver;
        _siteRepository = siteRepository;
    }

    public async Task<OperationResult<CreateKnowledgeSourceResult>> HandleAsync(CreateKnowledgeSourceCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();

        if (command.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            errors.Add("type", "Type is required.");
        }

        if (command.Type.Equals("Url", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(command.Url))
        {
            errors.Add("url", "Url is required for Url type.");
        }

        if (command.Type.Equals("Text", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(command.Text))
        {
            errors.Add("text", "Text is required for Text type.");
        }

        var normalizedType = NormalizeType(command.Type);
        if (normalizedType is null)
        {
            errors.Add("type", "Type must be Url, Text, or Pdf.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<CreateKnowledgeSourceResult>.ValidationFailed(errors);
        }

        var site = await _siteRepository.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<CreateKnowledgeSourceResult>.NotFound();
        }

        var botId = await _botResolver.GetOrCreateForSiteAsync(command.TenantId, command.SiteId, cancellationToken);
        var now = DateTime.UtcNow;
        var source = new KnowledgeSource
        {
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            BotId = botId,
            Type = normalizedType!,
            Name = command.Name,
            Url = command.Url,
            TextContent = command.Text,
            Status = IndexStatus.Queued,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sourceRepository.InsertSourceAsync(source, cancellationToken);
        return OperationResult<CreateKnowledgeSourceResult>.Success(new CreateKnowledgeSourceResult(source.Id, source.Status.ToString()));
    }

    private static string? NormalizeType(string type)
    {
        if (type.Equals("Url", StringComparison.OrdinalIgnoreCase)) return "Url";
        if (type.Equals("Text", StringComparison.OrdinalIgnoreCase)) return "Text";
        if (type.Equals("Pdf", StringComparison.OrdinalIgnoreCase)) return "Pdf";
        return null;
    }
}
