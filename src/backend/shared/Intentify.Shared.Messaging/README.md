# Intentify.Shared.Messaging

## Package purpose and scope
Defines shared in-process eventing abstractions and a default in-memory event bus implementation.

## Key public types/classes
- `IEvent`
- `IEventHandler<TEvent>`
- `IEventBus`
- `InMemoryEventBus`

## Dependencies and boundary notes
- Intended for cross-module integration via messaging abstractions.
- Avoid direct module-to-module infrastructure coupling when message-based integration fits.

## Known consumers (easy-to-verify)
- Used as a shared messaging abstraction for backend module integration patterns.

## Package-specific configuration
None.

## How to extend safely
- Keep contracts stable (`IEvent`, `IEventBus`) when adding bus features.
- Preserve deterministic handler invocation behavior for testability.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Messaging/tests/Intentify.Shared.Messaging.Tests/Intentify.Shared.Messaging.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/04-anti-circular-dependencies.md`
