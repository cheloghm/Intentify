# Backend

## What lives here
`src/backend` contains the .NET backend platform for Intentify:
- AppHost runtime composition (`app/Intentify.AppHost`)
- domain capability modules (`modules/Intentify.Modules.*`)
- cross-cutting shared packages (`shared/Intentify.Shared.*`)
- backend test projects for AppHost/modules/shared packages

## Architecture and composition (high level)
- AppHost composes startup, middleware, and module registration.
- Modules own capability-specific API/Application/Domain/Infrastructure concerns.
- Shared packages provide reusable building blocks (web, validation, security, data, observability, etc.) consumed by AppHost and modules.
- Module/layer-level details are documented in module READMEs; this file stays at backend-orientation level.

## How modules are organized
Module roots are under `modules/Intentify.Modules.<ModuleName>`.
Typical structure per module:
- `src/Intentify.Modules.<ModuleName>.Api`
- `src/Intentify.Modules.<ModuleName>.Application`
- `src/Intentify.Modules.<ModuleName>.Domain` (where present)
- `src/Intentify.Modules.<ModuleName>.Infrastructure`
- `tests/Intentify.Modules.<ModuleName>.Tests`

Current module set:
- Ads, Auth, Collector, Engage, Flows, Intelligence, Knowledge, Leads, PlatformAdmin, Promos, Sites, Tickets, Visitors.

## Shared package usage (high level)
Shared package roots are under `shared/Intentify.Shared.<Capability>` with paired test projects under `shared/*/tests/*Tests`.

Current shared package set:
- AI, Abstractions, Data.Mongo, KeyManagement, Messaging, Observability, Security, Testing, Validation, Web.

## Typical request flow (summary)
- Request enters AppHost pipeline.
- Request is routed to a module API endpoint.
- API delegates use-case execution to Application handlers.
- Application uses Domain models and Infrastructure adapters/repositories.
- Shared packages provide common capabilities (validation, web helpers, data, security, etc.).

For exact endpoint and handler maps, use module layer READMEs.

## Configuration/environment approach
Use `.env.example` as the baseline for local configuration and set environment variables as needed.

Common backend keys documented by this repo:
- `ASPNETCORE_ENVIRONMENT`
- `Intentify__Jwt__Issuer`
- `Intentify__Jwt__Audience`
- `Intentify__Jwt__SigningKey`
- `Intentify__Jwt__AccessTokenMinutes`
- `Intentify__Cors__AllowedOrigins`
- `Intentify__Mongo__ConnectionString`
- `Intentify__Mongo__DatabaseName`

## Where to make common backend changes
- Host-level startup/middleware/module wiring: `app/Intentify.AppHost`
- Capability behavior/endpoints: `modules/Intentify.Modules.<ModuleName>`
- Cross-cutting reusable behavior: `shared/Intentify.Shared.<Capability>`
- Tests: matching `tests/*Tests` project under AppHost/module/shared package roots

## How to locate API/Application/Domain/Infrastructure concerns
- Start from the target module root README in `modules/Intentify.Modules.<ModuleName>/README.md`.
- Then use layer READMEs in that module’s `src/Intentify.Modules.<ModuleName>.*` folders.
- Shared package-specific concerns are documented in `shared/Intentify.Shared.<Capability>/README.md`.

## Run/build/test pointers (verifiable)
From `src/backend`:

```bash
dotnet restore Intentify.sln
dotnet build -c Debug Intentify.sln --no-restore
dotnet test -c Debug Intentify.sln --no-build
```

Project-specific examples:

```bash
dotnet test app/Intentify.AppHost/tests/Intentify.AppHost.Tests/Intentify.AppHost.Tests.csproj
dotnet test modules/Intentify.Modules.Auth/tests/Intentify.Modules.Auth.Tests/Intentify.Modules.Auth.Tests.csproj
```

## Where deeper docs live
- AppHost README: [`app/Intentify.AppHost/README.md`](app/Intentify.AppHost/README.md)
- Modules index: [`modules/README.md`](modules/README.md)
- Shared packages index: [`shared/README.md`](shared/README.md)
- Engineering/testing policy: [`../../docs/codex/README.md`](../../docs/codex/README.md)

## Test and approval docs
- Test docs live in test project READMEs under:
  - `app/Intentify.AppHost/tests/*/README.md`
  - `modules/*/tests/*/README.md`
  - `shared/*/tests/*/README.md`
- No backend approvals-docs directory is currently verified in this repository tree.
