namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class GoogleSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google";

    public string BaseUrl { get; set; } = "https://example.com";

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class IntelligenceSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Search";

    public string Provider { get; set; } = "Google";
}
