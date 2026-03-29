public sealed class ResponseShaper
{
    public string Shape(string rawResponse, EngageConversationContext ctx)
    {
        // Enforce natural tone, at most one question, remove filler, make niche-relatable using knowledge + vocab
        // (full implementation reuses + extends existing ShapeAssistantResponse logic)
        return /* shaped natural response */;
    }
}
