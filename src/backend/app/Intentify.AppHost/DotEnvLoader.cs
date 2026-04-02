namespace Intentify.AppHost;

internal static class DotEnvLoader
{
    public static void Load()
    {
        var path = FindDotEnvPath();
        if (path is null)
        {
            Console.WriteLine("DotEnvLoader: no .env file found — using system environment only");
            return;
        }

        Console.WriteLine($"DotEnvLoader: loaded .env from {path}");

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            value = TrimQuotes(value);

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
                Console.WriteLine($"DotEnvLoader: set {key}={MaskValue(key, value)}");
            }
        }
    }

    private static string MaskValue(string key, string value)
    {
        if (key.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            return value.Length > 8 ? value[..8] + "***" : "***";
        }
        return value;
    }

    private static string? FindDotEnvPath()
    {
        var start = Directory.GetCurrentDirectory();
        Console.WriteLine($"DotEnvLoader: starting directory walk from: {start}");

        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            Console.WriteLine($"DotEnvLoader: checking {candidate}");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }
}
