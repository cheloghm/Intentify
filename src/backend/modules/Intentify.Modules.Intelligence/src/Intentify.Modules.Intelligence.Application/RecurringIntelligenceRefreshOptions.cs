namespace Intentify.Modules.Intelligence.Application;

public sealed class RecurringIntelligenceRefreshOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:RecurringRefresh";

    public bool Enabled { get; init; }

    public int PollIntervalSeconds { get; init; } = 300;

    public string TimeWindow { get; init; } = "7d";

    public int MaxProfilesPerCycle { get; init; } = 100;
}
