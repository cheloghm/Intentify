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
            bot.FallbackStyle,
            bot.BusinessDescription,
            bot.Industry,
            bot.ServicesDescription,
            bot.GeoFocus,
            bot.PersonalityDescriptor,
            bot.DigestEmailEnabled,
            bot.DigestEmailRecipients,
            bot.DigestEmailFrequency,
            HideBranding: bot.HideBranding,
            CustomBrandingText: bot.CustomBrandingText,
            AbTestEnabled: bot.AbTestEnabled,
            OpeningMessageA: bot.OpeningMessageA,
            OpeningMessageB: bot.OpeningMessageB,
            AbTestImpressionCountA: bot.AbTestImpressionCountA,
            AbTestImpressionCountB: bot.AbTestImpressionCountB,
            AbTestConversionCountA: bot.AbTestConversionCountA,
            AbTestConversionCountB: bot.AbTestConversionCountB,
            SurveyEnabled: bot.SurveyEnabled,
            SurveyQuestion: bot.SurveyQuestion,
            SurveyOptions: bot.SurveyOptions,
            ExitIntentEnabled: bot.ExitIntentEnabled,
            ExitIntentMessage: bot.ExitIntentMessage));
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
            command.BusinessDescription,
            command.Industry,
            command.ServicesDescription,
            command.GeoFocus,
            command.PersonalityDescriptor,
            command.DigestEmailEnabled,
            command.DigestEmailRecipients,
            command.DigestEmailFrequency,
            command.HideBranding,
            command.CustomBrandingText,
            command.AbTestEnabled,
            command.OpeningMessageA,
            command.OpeningMessageB,
            command.SurveyEnabled,
            command.SurveyQuestion,
            command.SurveyOptions,
            command.ExitIntentEnabled,
            command.ExitIntentMessage,
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
            updated.FallbackStyle,
            updated.BusinessDescription,
            updated.Industry,
            updated.ServicesDescription,
            updated.GeoFocus,
            updated.PersonalityDescriptor,
            updated.DigestEmailEnabled,
            updated.DigestEmailRecipients,
            updated.DigestEmailFrequency,
            HideBranding: updated.HideBranding,
            CustomBrandingText: updated.CustomBrandingText,
            AbTestEnabled: updated.AbTestEnabled,
            OpeningMessageA: updated.OpeningMessageA,
            OpeningMessageB: updated.OpeningMessageB,
            AbTestImpressionCountA: updated.AbTestImpressionCountA,
            AbTestImpressionCountB: updated.AbTestImpressionCountB,
            AbTestConversionCountA: updated.AbTestConversionCountA,
            AbTestConversionCountB: updated.AbTestConversionCountB,
            SurveyEnabled: updated.SurveyEnabled,
            SurveyQuestion: updated.SurveyQuestion,
            SurveyOptions: updated.SurveyOptions,
            ExitIntentEnabled: updated.ExitIntentEnabled,
            ExitIntentMessage: updated.ExitIntentMessage));
    }
}
