using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class GetEngageBotHandler
{
    private readonly IEngageBotRepository _repository;

    public GetEngageBotHandler(IEngageBotRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResult<EngageBotResult>> HandleAsync(GetEngageBotQuery query, CancellationToken cancellationToken = default)
    {
        var bot = await _repository.GetBySiteAsync(query.TenantId, query.SiteId, cancellationToken);
        if (bot is null)
        {
            return OperationResult<EngageBotResult>.NotFound();
        }

        var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(
            bot.BotId,
            string.IsNullOrWhiteSpace(resolvedName) ? "Assistant" : resolvedName,
            bot.PrimaryColor,
            bot.LauncherVisible,
            bot.Tone,
            bot.Verbosity,
            bot.FallbackStyle));
    }
}

public sealed class UpdateEngageBotHandler
{
    private readonly IEngageBotRepository _repository;

    public UpdateEngageBotHandler(IEngageBotRepository repository)
    {
        _repository = repository;
    }

    public async Task<OperationResult<EngageBotResult>> HandleAsync(UpdateEngageBotCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors.Add("name", "Name is required.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<EngageBotResult>.ValidationFailed(errors);
        }

        var updated = await _repository.UpdateSettingsAsync(
            command.TenantId,
            command.SiteId,
            command.Name,
            command.PrimaryColor,
            command.LauncherVisible,
            command.Tone,
            command.Verbosity,
            command.FallbackStyle,
            cancellationToken);

        if (updated is null)
        {
            return OperationResult<EngageBotResult>.NotFound();
        }

        var resolvedName = string.IsNullOrWhiteSpace(updated.Name) ? updated.DisplayName : updated.Name;

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(
            updated.BotId,
            string.IsNullOrWhiteSpace(resolvedName) ? "Assistant" : resolvedName,
            updated.PrimaryColor,
            updated.LauncherVisible,
            updated.Tone,
            updated.Verbosity,
            updated.FallbackStyle));
    }
}
