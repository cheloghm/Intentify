# Intentify.AppHost

## AppHost role in platform
`Intentify.AppHost` is the backend web host/composition layer. It is responsible for:
- constructing application startup.
- registering and wiring backend modules.
- applying host-level middleware and policies.

Module business logic and route details remain inside module projects.

## Startup flow
1. `Program.cs` creates a builder via `AppHostApplication.CreateBuilder(args)`.
2. AppHost loads environment values (including `.env` support via `DotEnvLoader`).
3. `AppHostApplication.Build(builder)` configures services and middleware.
4. AppHost maps host-level and module endpoints, then runs the app.

## Module registration and endpoint mapping
- Module registration/mapping is centralized in `AppModules.cs`.
- Modules are collected through `AppModuleCatalog` and registered via `AddAppModules(...)`.
- Endpoint mapping delegates to registered modules via `MapAppModules()`.

To add/remove a module at host level, update the module list in `AppModuleCatalog`.

## Cross-cutting middleware and policies
AppHost applies platform-level concerns, including:
- OpenAPI/Swagger generation (development environment).
- JWT authentication setup.
- authorization policy registration (including platform admin policy).
- CORS policy setup and middleware.
- health endpoint (`/health`).
- debug endpoint mapping via `MapDebugEndpoints()`.

### Debug endpoint note
Debug endpoint behavior is intended for development/local diagnostics and must not be treated as a production-exposed operational surface. Production behavior is validated by AppHost tests.

## AppHost configuration keys and fallback behavior
Host-level configuration read by AppHost includes:
- `Intentify__Jwt__Issuer`
- `Intentify__Jwt__Audience`
- `Intentify__Jwt__SigningKey`
- `Intentify__Jwt__AccessTokenMinutes`
- `Intentify__Cors__AllowedOrigins`
- `Intentify__Mongo__ConnectionString`
- `Intentify__Mongo__DatabaseName`

Behavior highlights:
- JWT settings are required for authentication configuration.
- CORS allowed origins are required by host setup.
- Mongo settings are required unless host applies local/dev fallback behavior.

For broader backend configuration guidance, see `src/backend/README.md`.

## How to add/remove module wiring
Host-level module wiring is controlled in AppHost only:
1. Add/remove module entry in `AppModuleCatalog` (`AppModules.cs`).
2. Ensure the AppHost project references the module API project in `Intentify.AppHost.csproj`.
3. Keep module-internal implementation details inside module docs/projects.

## AppHost test commands
From `src/backend`:

```bash
dotnet test app/Intentify.AppHost/tests/Intentify.AppHost.Tests/Intentify.AppHost.Tests.csproj
```

Solution-level validation:

```bash
dotnet test -c Debug Intentify.sln --no-build
```

## Links to related docs/code
- Backend overview: [`../../README.md`](../../README.md)
- Modules overview: [`../../modules/README.md`](../../modules/README.md)
- Shared packages overview: [`../../shared/README.md`](../../shared/README.md)
- Architecture boundaries reference: [`../../../../docs/codex/01-architecture-boundaries.md`](../../../../docs/codex/01-architecture-boundaries.md)

Key AppHost source files:
- `Program.cs`
- `AppHostApplication.cs`
- `AppModules.cs`
- `DebugEndpoints.cs`
