# Intentify.Shared.Observability

## Package purpose and scope
Provides shared host-level observability wiring helpers.

## Key public types/classes
- `HostApplicationBuilderExtensions`

## Dependencies and boundary notes
- Shared package for common observability registration.
- Keep it focused on reusable setup extensions, not module-specific logging behavior.

## Known consumers (easy-to-verify)
- Intended for host/service startup composition where shared observability defaults are required.

## Package-specific configuration
No package-owned configuration section is currently enforced directly by this package.

## How to extend safely
- Add composable extension methods.
- Keep observability defaults reusable and non-module-specific.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Observability/tests/Intentify.Shared.Observability.Tests/Intentify.Shared.Observability.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/03-testing-playbook.md`
