namespace Intentify.Modules.Intelligence.Infrastructure;

// ── Primary provider: SerpApi Google Trends ───────────────────────────────────
// Configuration section maps to .env:
//   Intentify__Intelligence__SerpApi__BaseUrl=https://serpapi.com/
//   Intentify__Intelligence__SerpApi__ApiKey=YOUR_SERPAPI_KEY
//   Intentify__Intelligence__SerpApi__TimeoutSeconds=15

public sealed class SerpApiTrendsOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:SerpApi";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 15;
}

// ── Provider selection ────────────────────────────────────────────────────────
// Currently only SerpApi is supported. Kept as a config option so switching
// providers in the future requires only an env change, not a code change.

public sealed class IntelligenceSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Search";

    // Supported: "SerpApi" (default)
    public string Provider { get; set; } = "SerpApi";
}

// ── Legacy stubs — kept so nothing else breaks at compile time ────────────────
// These are no longer used by IntelligenceModule but may be referenced by
// existing integration tests. They can be removed in a future cleanup pass.

[Obsolete("Use SerpApiTrendsOptions instead. Remove this after cleaning up tests.")]
public sealed class GoogleSearchOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Search";
    public string? BaseUrl { get; set; }
    public string? ApiKey  { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

[Obsolete("Use SerpApiTrendsOptions instead. Remove this after cleaning up tests.")]
public sealed class GoogleTrendsOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Trends";
    public string? BaseUrl { get; set; }
    public string? ApiKey  { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

[Obsolete("Not used in initial release. Google Ads Demographics overlay is planned for Phase 3.")]
public sealed class GoogleAdsOptions
{
    public const string ConfigurationSection = "Intentify:Intelligence:Google:Ads";
    public string? BaseUrl        { get; set; }
    public string? DeveloperToken { get; set; }
    public string? ClientId       { get; set; }
    public string? ClientSecret   { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}
