namespace Intentify.Modules.Knowledge.Application;

public interface IKnowledgeChunker
{
    IReadOnlyList<string> Chunk(string text, int maxChunkLength = 600);
}

public sealed class KnowledgeChunker : IKnowledgeChunker
{
    public IReadOnlyList<string> Chunk(string text, int maxChunkLength = 600)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var paragraphs = text
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();

        var sections = BuildSections(paragraphs);
        var chunks = new List<string>();

        foreach (var section in sections)
        {
            var current = string.Empty;
            var headingPrefix = string.IsNullOrEmpty(section.Heading)
                ? string.Empty
                : $"{section.Heading}\n\n";

            foreach (var paragraph in section.Paragraphs)
            {
                var paragraphParts = SplitToLength(paragraph, Math.Max(1, maxChunkLength - headingPrefix.Length));

                foreach (var part in paragraphParts)
                {
                    var candidate = headingPrefix + part;

                    if (current.Length == 0)
                    {
                        current = candidate.Length <= maxChunkLength
                            ? candidate
                            : candidate[..maxChunkLength].TrimEnd();
                        continue;
                    }

                    var appended = $"{current}\n\n{part}";
                    if (appended.Length <= maxChunkLength)
                    {
                        current = appended;
                        continue;
                    }

                    chunks.Add(current);
                    current = candidate.Length <= maxChunkLength
                        ? candidate
                        : candidate[..maxChunkLength].TrimEnd();
                }
            }

            if (current.Length > maxChunkLength)
            {
                current = current[..maxChunkLength].TrimEnd();
            }

            if (current.Length > 0)
            {
                chunks.Add(current);
            }
        }

        return chunks;
    }

    private static IReadOnlyCollection<SectionBuffer> BuildSections(IReadOnlyCollection<string> paragraphs)
    {
        var sections = new List<SectionBuffer>();
        var current = new SectionBuffer(null);

        foreach (var paragraph in paragraphs)
        {
            if (IsHeadingParagraph(paragraph))
            {
                if (current.Paragraphs.Count > 0)
                {
                    sections.Add(current);
                }

                current = new SectionBuffer(NormalizeHeading(paragraph));
                continue;
            }

            current.Paragraphs.Add(paragraph);
        }

        if (current.Paragraphs.Count > 0)
        {
            sections.Add(current);
        }

        return sections.Count == 0 ? [new SectionBuffer(null)] : sections;
    }

    private static bool IsHeadingParagraph(string paragraph)
    {
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            return false;
        }

        if (paragraph.StartsWith("# ", StringComparison.Ordinal))
        {
            return true;
        }

        return paragraph.Length <= 80
            && paragraph.EndsWith(':', StringComparison.Ordinal)
            && !paragraph.Contains('.', StringComparison.Ordinal);
    }

    private static string NormalizeHeading(string heading)
    {
        if (heading.StartsWith("# ", StringComparison.Ordinal))
        {
            return heading.Trim();
        }

        return $"# {heading.TrimEnd(':').Trim()}";
    }

    private static IReadOnlyCollection<string> SplitToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (value.Length <= maxLength)
        {
            return [value];
        }

        var parts = new List<string>();
        var remaining = value.Trim();

        while (remaining.Length > maxLength)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxLength);
            if (splitIndex <= 0)
            {
                splitIndex = maxLength;
            }

            var part = remaining[..splitIndex].Trim();
            if (part.Length == 0)
            {
                part = remaining[..Math.Min(maxLength, remaining.Length)].Trim();
            }

            if (part.Length > 0)
            {
                parts.Add(part);
            }

            remaining = remaining[Math.Min(splitIndex, remaining.Length)..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            parts.Add(remaining);
        }

        return parts;
    }

    private sealed class SectionBuffer(string? heading)
    {
        public string? Heading { get; } = heading;

        public List<string> Paragraphs { get; } = [];
    }
}
