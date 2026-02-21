using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class GetEngageBotHandler
{
    private readonly ISiteRepository _siteRepository;
    private readonly IEngageBotRepository _botRepository;

    public GetEngageBotHandler(ISiteRepository siteRepository, IEngageBotRepository botRepository)
    {
        _siteRepository = siteRepository;
        _botRepository = botRepository;
    }

    public async Task<OperationResult<EngageBotResult>> HandleAsync(GetEngageBotQuery query, CancellationToken cancellationToken = default)
    {
        var site = await _siteRepository.GetByTenantAndIdAsync(query.TenantId, query.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<EngageBotResult>.NotFound();
        }

        var bot = await _botRepository.GetOrCreateForSiteAsync(query.TenantId, query.SiteId, cancellationToken);
        var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = "Assistant";
        }

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(bot.BotId, resolvedName));
    }
}

public sealed class UpdateEngageBotHandler
{
    private const int MaxNameLength = 50;
    private readonly ISiteRepository _siteRepository;
    private readonly IEngageBotRepository _botRepository;

    public UpdateEngageBotHandler(ISiteRepository siteRepository, IEngageBotRepository botRepository)
    {
        _siteRepository = siteRepository;
        _botRepository = botRepository;
    }

    public async Task<OperationResult<EngageBotResult>> HandleAsync(UpdateEngageBotCommand command, CancellationToken cancellationToken = default)
    {
        var validationErrors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            validationErrors.Add("name", "Name is required.");
        }
        else if (command.Name.Trim().Length > MaxNameLength)
        {
            validationErrors.Add("name", $"Name must be {MaxNameLength} characters or fewer.");
        }

        if (validationErrors.HasErrors)
        {
            return OperationResult<EngageBotResult>.ValidationFailed(validationErrors);
        }

        var site = await _siteRepository.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null)
        {
            return OperationResult<EngageBotResult>.NotFound();
        }

        var updated = await _botRepository.UpdateNameAsync(command.TenantId, command.SiteId, command.Name, cancellationToken)
            ?? await _botRepository.GetOrCreateForSiteAsync(command.TenantId, command.SiteId, cancellationToken);

        var resolvedName = string.IsNullOrWhiteSpace(updated.Name) ? updated.DisplayName : updated.Name;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = "Assistant";
        }

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(updated.BotId, resolvedName));
    }
}
