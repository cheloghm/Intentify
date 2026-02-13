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

        var chunks = new List<string>();
        var current = string.Empty;

        foreach (var paragraph in paragraphs)
        {
            if (current.Length == 0)
            {
                current = paragraph;
                continue;
            }

            if (current.Length + 2 + paragraph.Length <= maxChunkLength)
            {
                current += "\n\n" + paragraph;
                continue;
            }

            chunks.Add(current);
            current = paragraph;
        }

        if (current.Length > 0)
        {
            chunks.Add(current);
        }

        return chunks;
    }
}
