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

            foreach (var paragraph in section.Paragraphs)
            {
                var candidate = string.IsNullOrEmpty(section.Heading)
                    ? paragraph
                    : $"{section.Heading}\n\n{paragraph}";

                if (current.Length == 0)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length + 2 + paragraph.Length <= maxChunkLength)
                {
                    current += "\n\n" + paragraph;
                    continue;
                }

                chunks.Add(current);

                // Keep a lightweight overlap paragraph to preserve local context.
                var overlap = TryBuildOverlap(paragraph, maxChunkLength);
                current = string.IsNullOrEmpty(section.Heading)
                    ? overlap
                    : $"{section.Heading}\n\n{overlap}";
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

    private static string TryBuildOverlap(string paragraph, int maxChunkLength)
    {
        return paragraph;
    }

    private sealed class SectionBuffer(string? heading)
    {
        public string? Heading { get; } = heading;

        public List<string> Paragraphs { get; } = [];
    }
}
