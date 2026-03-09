# Intentify.Shared.KeyManagement

## Package purpose and scope
Provides shared key generation abstractions and implementation used for site/widget key workflows.

## Key public types/classes
- `IKeyGenerator`
- `KeyGenerator`
- `KeyPurpose`

## Dependencies and boundary notes
- Shared package for key creation primitives.
- Keep key lifecycle policy decisions in module/application layers, not this package.

## Known consumers (easy-to-verify)
- `Intentify.Modules.Sites` registers and uses `IKeyGenerator`/`KeyGenerator`.

## Package-specific configuration
None.

## How to extend safely
- Add key-related primitives that remain module-agnostic.
- Preserve deterministic/clear key-purpose semantics through `KeyPurpose`.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.KeyManagement/tests/Intentify.Shared.KeyManagement.Tests/Intentify.Shared.KeyManagement.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/04-anti-circular-dependencies.md`
