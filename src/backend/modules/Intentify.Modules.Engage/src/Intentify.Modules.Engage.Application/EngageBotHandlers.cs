using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using System.Text.RegularExpressions;

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

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(bot.BotId, resolvedName, bot.PrimaryColor, bot.LauncherVisible, bot.Tone, bot.Verbosity, bot.FallbackStyle));
    }
}

public sealed class UpdateEngageBotHandler
{
    private const int MaxNameLength = 50;
    private static readonly HashSet<string> AllowedTones = new(StringComparer.OrdinalIgnoreCase) { "warm", "professional", "casual" };
    private static readonly HashSet<string> AllowedVerbosity = new(StringComparer.OrdinalIgnoreCase) { "brief", "balanced", "detailed" };
    private static readonly HashSet<string> AllowedFallbackStyles = new(StringComparer.OrdinalIgnoreCase) { "refine", "handoff" };
    private static readonly Regex PrimaryColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
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

        var primaryColor = string.IsNullOrWhiteSpace(command.PrimaryColor) ? null : command.PrimaryColor.Trim();
        if (primaryColor is not null && !PrimaryColorRegex.IsMatch(primaryColor))
        {
            validationErrors.Add("primaryColor", "Primary color must be a hex color in #RRGGBB format.");
        }

        var tone = NormalizeOptional(command.Tone);
        if (tone is not null && !AllowedTones.Contains(tone))
        {
            validationErrors.Add("tone", "Tone must be one of: warm, professional, casual.");
        }

        var verbosity = NormalizeOptional(command.Verbosity);
        if (verbosity is not null && !AllowedVerbosity.Contains(verbosity))
        {
            validationErrors.Add("verbosity", "Verbosity must be one of: brief, balanced, detailed.");
        }

        var fallbackStyle = NormalizeOptional(command.FallbackStyle);
        if (fallbackStyle is not null && !AllowedFallbackStyles.Contains(fallbackStyle))
        {
            validationErrors.Add("fallbackStyle", "Fallback style must be one of: refine, handoff.");
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

        var updated = await _botRepository.UpdateSettingsAsync(command.TenantId, command.SiteId, command.Name, primaryColor, command.LauncherVisible, tone, verbosity, fallbackStyle, cancellationToken)
            ?? await _botRepository.GetOrCreateForSiteAsync(command.TenantId, command.SiteId, cancellationToken);

        var resolvedName = string.IsNullOrWhiteSpace(updated.Name) ? updated.DisplayName : updated.Name;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = "Assistant";
        }

        return OperationResult<EngageBotResult>.Success(new EngageBotResult(updated.BotId, resolvedName, updated.PrimaryColor, updated.LauncherVisible, updated.Tone, updated.Verbosity, updated.FallbackStyle));
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
