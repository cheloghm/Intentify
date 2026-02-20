namespace Intentify.Modules.Knowledge.Infrastructure;

public sealed class OpenSearchOptions
{
    public const string ConfigurationSection = "Intentify:OpenSearch";

    public bool Enabled { get; set; }

    public string Url { get; set; } = "http://localhost:9200";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string IndexName { get; set; } = "intentify-knowledge-chunks";

    public int RequestTimeoutSeconds { get; set; } = 10;
}
