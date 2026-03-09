# Intentify.Modules.Tickets

## Module purpose and bounded context
Tickets manages ticket lifecycle, assignment, notes, and status transitions for authenticated operations.

## Capability summary (route groups)
- Base route group: `/tickets`
- High-level capabilities:
  - ticket create/list/get/update
  - assignment updates
  - note add/list
  - status transitions

## Layer map
- API: `src/Intentify.Modules.Tickets.Api`
- Application: `src/Intentify.Modules.Tickets.Application`
- Domain: `src/Intentify.Modules.Tickets.Domain`
- Infrastructure: `src/Intentify.Modules.Tickets.Infrastructure`
- Tests: `tests/Intentify.Modules.Tickets.Tests`

## Module-specific configuration
No Tickets-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Auth filter: `RequireAuthFilter`
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Tickets.Api/TicketsModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Tickets.Api/*`
- Ticket handlers/contracts: `src/Intentify.Modules.Tickets.Application/*`
- Domain models: `src/Intentify.Modules.Tickets.Domain/*`
- Repositories: `src/Intentify.Modules.Tickets.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Tickets/tests/Intentify.Modules.Tickets.Tests/Intentify.Modules.Tickets.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Tickets.Api/README.md`
- `src/Intentify.Modules.Tickets.Application/README.md`
- `src/Intentify.Modules.Tickets.Domain/README.md`
- `src/Intentify.Modules.Tickets.Infrastructure/README.md`
- `tests/Intentify.Modules.Tickets.Tests/README.md`
