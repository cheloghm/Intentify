# Intentify.Shared.AI

## Package purpose and scope
Provides shared AI client abstractions and option types used by modules that integrate with chat completion/embedding workflows.

## Key public types/classes
- `AiOptions`
- `IChatCompletionClient`
- `IEmbeddingClient`
- `HttpChatCompletionClient`
- `NullChatCompletionClient` / `NullEmbeddingClient`

## Dependencies and boundary notes
- Shared package intended for cross-module AI integration plumbing.
- Avoid module-specific business logic in this package.

## Known consumers (easy-to-verify)
- `Intentify.Modules.Engage` uses `AiOptions`, `IChatCompletionClient`, and `HttpChatCompletionClient` in module registration.

## Package-specific configuration
No configuration is read directly inside this package.
Consumers typically populate `AiOptions` (for example, base URL/API key) from module configuration.

## How to extend safely
- Add new provider/client implementations behind existing interfaces.
- Keep transport/provider concerns in shared AI package and module behavior in module application layers.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.AI/tests/Intentify.Shared.AI.Tests/Intentify.Shared.AI.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/03-testing-playbook.md`
