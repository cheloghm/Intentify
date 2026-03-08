namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class GoogleSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Search";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class GoogleTrendsOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Trends";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class GoogleAdsOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Ads";

    public string? BaseUrl { get; set; }

    public string? DeveloperToken { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? CustomerId { get; set; }

    public string? LoginCustomerId { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class IntelligenceSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Search";

    public string Provider { get; set; } = "Google";
}
