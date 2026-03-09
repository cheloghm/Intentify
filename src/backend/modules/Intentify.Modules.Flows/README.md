# Intentify.Modules.Flows

## Module purpose and bounded context
Flows manages flow definitions, enable/disable lifecycle, and flow run history for authenticated operations.

## Capability summary (route groups)
- Base route group: `/flows`
- High-level capabilities:
  - create/update flows
  - enable/disable flows
  - list/get flows
  - list flow runs

## Layer map
- API: `src/Intentify.Modules.Flows.Api`
- Application: `src/Intentify.Modules.Flows.Application`
- Domain: `src/Intentify.Modules.Flows.Domain`
- Infrastructure: `src/Intentify.Modules.Flows.Infrastructure`
- Tests: `tests/Intentify.Modules.Flows.Tests`

## Module-specific configuration
No Flows-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Auth filter: `RequireAuthFilter`
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Flows.Api/FlowsModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Flows.Api/*`
- Flow services/contracts: `src/Intentify.Modules.Flows.Application/*`
- Domain models: `src/Intentify.Modules.Flows.Domain/*`
- Repositories: `src/Intentify.Modules.Flows.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Flows/tests/Intentify.Modules.Flows.Tests/Intentify.Modules.Flows.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Flows.Api/README.md`
- `src/Intentify.Modules.Flows.Application/README.md`
- `src/Intentify.Modules.Flows.Domain/README.md`
- `src/Intentify.Modules.Flows.Infrastructure/README.md`
- `tests/Intentify.Modules.Flows.Tests/README.md`
