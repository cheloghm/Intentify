# Backend

## Backend purpose and scope
`src/backend` hosts the .NET backend platform:
- AppHost web entrypoint/composition layer.
- capability modules under `modules/Intentify.Modules.*`.
- reusable shared backend packages under `shared/Intentify.Shared.*`.
- backend test projects for AppHost/modules/shared packages.

## Solution layout and naming conventions
- Solution: `Intentify.sln`
- App host project: `app/Intentify.AppHost`
- Modules: `modules/Intentify.Modules.<ModuleName>`
  - typical layer projects:
    - `src/Intentify.Modules.<ModuleName>.Api`
    - `src/Intentify.Modules.<ModuleName>.Application`
    - `src/Intentify.Modules.<ModuleName>.Domain` (where present)
    - `src/Intentify.Modules.<ModuleName>.Infrastructure`
  - tests: `tests/Intentify.Modules.<ModuleName>.Tests`
- Shared packages: `shared/Intentify.Shared.<Capability>`
  - source: `src/Intentify.Shared.<Capability>`
  - tests: `tests/Intentify.Shared.<Capability>.Tests`

## AppHost composition model
AppHost is the runtime composition layer:
- builds web application startup and middleware.
- registers modules implementing the shared app-module interface.
- maps module endpoints through a central registry.
- applies auth, authorization policy, CORS, health endpoint, and debug endpoint behavior.

See AppHost source docs (added in later batches) under `app/Intentify.AppHost`.

## Module catalog
Current module roots in repository:
- `Intentify.Modules.Ads`
- `Intentify.Modules.Auth`
- `Intentify.Modules.Collector`
- `Intentify.Modules.Engage`
- `Intentify.Modules.Flows`
- `Intentify.Modules.Intelligence`
- `Intentify.Modules.Knowledge`
- `Intentify.Modules.Leads`
- `Intentify.Modules.PlatformAdmin`
- `Intentify.Modules.Promos`
- `Intentify.Modules.Sites`
- `Intentify.Modules.Tickets`
- `Intentify.Modules.Visitors`

Route-level details are intentionally kept out of this high-level README.

## Shared package catalog
Current shared package roots in repository:
- `Intentify.Shared.AI`
- `Intentify.Shared.Abstractions`
- `Intentify.Shared.Data.Mongo`
- `Intentify.Shared.KeyManagement`
- `Intentify.Shared.Messaging`
- `Intentify.Shared.Observability`
- `Intentify.Shared.Security`
- `Intentify.Shared.Testing`
- `Intentify.Shared.Validation`
- `Intentify.Shared.Web`

## Dependency/layer boundaries summary
Backend follows module-layer boundaries and testing guardrails defined in `docs/codex`.
Use these canonical references instead of duplicating policy text here:
- architecture boundaries: [`../../docs/codex/01-architecture-boundaries.md`](../../docs/codex/01-architecture-boundaries.md)
- testing playbook: [`../../docs/codex/03-testing-playbook.md`](../../docs/codex/03-testing-playbook.md)

## Detailed backend configuration
### Environment variables and key settings
Use `.env` for local machine values (with `.env.example` as starter reference).

Common backend keys used by AppHost and module startup:
- `ASPNETCORE_ENVIRONMENT` (example in `.env.example`)
- `Intentify__Jwt__Issuer`
- `Intentify__Jwt__Audience`
- `Intentify__Jwt__SigningKey`
- `Intentify__Jwt__AccessTokenMinutes`
- `Intentify__Cors__AllowedOrigins` (comma-separated origins)
- `Intentify__Mongo__ConnectionString`
- `Intentify__Mongo__DatabaseName`

### Behavior notes
- JWT issuer/audience/signing key are required for authentication setup.
- CORS allowed origins are required; startup fails when missing.
- Mongo connection/database:
  - if explicitly configured, those values are used.
  - for local/dev scenarios, AppHost may apply fallback defaults when config is absent.

## Run/build/test commands
From `src/backend`:

```bash
dotnet restore Intentify.sln
dotnet build -c Debug Intentify.sln --no-restore
dotnet test -c Debug Intentify.sln --no-build
```

Useful project-scoped examples:

```bash
dotnet test app/Intentify.AppHost/tests/Intentify.AppHost.Tests/Intentify.AppHost.Tests.csproj
dotnet test modules/Intentify.Modules.Auth/tests/Intentify.Modules.Auth.Tests/Intentify.Modules.Auth.Tests.csproj
```

## Troubleshooting
- **Startup error: JWT is not configured**
  - Ensure `Intentify__Jwt__Issuer`, `Intentify__Jwt__Audience`, and `Intentify__Jwt__SigningKey` are set.
- **Startup error: CORS is not configured**
  - Ensure `Intentify__Cors__AllowedOrigins` is set to allowed frontend origins.
- **Mongo configuration issues**
  - Set `Intentify__Mongo__ConnectionString` and `Intentify__Mongo__DatabaseName` explicitly for predictable behavior.
- **Test project package errors**
  - Ensure test projects keep required test package references (guarded by `Directory.Build.props`).

## Links to AppHost/modules/shared docs
- AppHost folder: [`app/Intentify.AppHost/`](app/Intentify.AppHost/)
- Modules folder: [`modules/`](modules/)
- Shared packages folder: [`shared/`](shared/)
- Modules overview README (placeholder to be expanded in later batch): [`modules/README.md`](modules/README.md)
- Shared overview README (placeholder to be expanded in later batch): [`shared/README.md`](shared/README.md)
