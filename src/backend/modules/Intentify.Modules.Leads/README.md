# Intentify.Modules.Leads

## Module purpose and bounded context
Leads manages lead retrieval and lead linkage/orchestration used by surrounding promo/engagement workflows.

## Capability summary (route groups)
- Base route group: `/leads`
- High-level capabilities:
  - list leads
  - get lead detail

## Layer map
- API: `src/Intentify.Modules.Leads.Api`
- Application: `src/Intentify.Modules.Leads.Application`
- Domain: `src/Intentify.Modules.Leads.Domain`
- Infrastructure: `src/Intentify.Modules.Leads.Infrastructure`
- Tests: `tests/Intentify.Modules.Leads.Tests`

## Module-specific configuration
No Leads-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Auth filter: `RequireAuthFilter`
- Cross-module use via `ILeadVisitorLinker`
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Leads.Api/LeadsModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Leads.Api/*`
- Lead contracts/handlers: `src/Intentify.Modules.Leads.Application/*`
- Domain models: `src/Intentify.Modules.Leads.Domain/*`
- Repositories: `src/Intentify.Modules.Leads.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Leads/tests/Intentify.Modules.Leads.Tests/Intentify.Modules.Leads.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Leads.Api/README.md`
- `src/Intentify.Modules.Leads.Application/README.md`
- `src/Intentify.Modules.Leads.Domain/README.md`
- `src/Intentify.Modules.Leads.Infrastructure/README.md`
- `tests/Intentify.Modules.Leads.Tests/README.md`
