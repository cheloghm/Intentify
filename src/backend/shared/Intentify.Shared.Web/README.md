# Intentify.Shared.Web

## Package purpose and scope
Provides shared web-layer abstractions/helpers used by AppHost and module API layers.

## Key public types/classes
- `IAppModule` (module registration/mapping abstraction)
- `CorrelationIdMiddleware`
- `ProblemDetailsHelpers`

## Dependencies and boundary notes
- Shared package for reusable host/API web concerns.
- Keep module business logic outside this package.

## Known consumers (easy-to-verify)
- `Intentify.AppHost` uses `IAppModule` composition patterns.
- Backend module API projects implement `IAppModule`.

## Package-specific configuration
No package-owned configuration section is currently required directly by this package.

## How to extend safely
- Keep abstractions stable (`IAppModule`) to avoid broad module breakage.
- Add helper utilities only when shared by multiple modules/host components.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Web/tests/Intentify.Shared.Web.Tests/Intentify.Shared.Web.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/04-anti-circular-dependencies.md`
