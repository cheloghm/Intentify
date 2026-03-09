# Intentify.Modules.PlatformAdmin

## Module purpose and bounded context
PlatformAdmin provides platform-level administrative read endpoints protected by a dedicated authorization policy.

## Capability summary (route groups)
- Base route group: `/platform-admin`
- High-level capabilities:
  - platform summary retrieval
  - tenant list/detail retrieval
  - operational summary retrieval

## Layer map
Current repository shape:
- API: `src/Intentify.Modules.PlatformAdmin.Api`
- Application: `src/Intentify.Modules.PlatformAdmin.Application`
- Infrastructure: `src/Intentify.Modules.PlatformAdmin.Infrastructure`

## Module-specific configuration
No PlatformAdmin-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Authorization policy dependency: `PlatformAdmin` policy (host-level registration)
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.PlatformAdmin.Api/PlatformAdminModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.PlatformAdmin.Api/*`
- Read handlers/contracts: `src/Intentify.Modules.PlatformAdmin.Application/*`
- Read repository implementation: `src/Intentify.Modules.PlatformAdmin.Infrastructure/*`

## Test coverage and commands
Current implementation state:
- no module test project is currently present under `tests/Intentify.Modules.PlatformAdmin.Tests`.

For solution-level coverage from `src/backend`:

```bash
dotnet test -c Debug Intentify.sln --no-build
```

## Known gaps/current state notes
- No `Domain` layer project is currently present in this module.
- No module-specific test project is currently present in this module.

## Links to layer READMEs
- `src/Intentify.Modules.PlatformAdmin.Api/README.md`
- `src/Intentify.Modules.PlatformAdmin.Application/README.md`
- `src/Intentify.Modules.PlatformAdmin.Infrastructure/README.md`
