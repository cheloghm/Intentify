# Intentify.Shared.Abstractions

## Package purpose and scope
Contains small cross-cutting primitives intended to be reused across modules without introducing infrastructure coupling.

## Key public types/classes
- `Clock`
- `Result`
- `Pagination` (`PageRequest`, `PageResult<T>`)
- `StrongId<TValue, TSelf>`

## Dependencies and boundary notes
- Keep this package lightweight and dependency-minimal.
- Suitable for reusable primitives, not module-specific policies.

## Known consumers (easy-to-verify)
- Used broadly by backend modules and shared packages as a common primitives layer.

## Package-specific configuration
None.

## How to extend safely
- Add only general-purpose primitives with stable semantics.
- Avoid introducing references to infrastructure/web/data providers here.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Abstractions/tests/Intentify.Shared.Abstractions.Tests/Intentify.Shared.Abstractions.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/04-anti-circular-dependencies.md`
