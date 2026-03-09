# Intentify.Modules.Auth

## Module purpose and bounded context
Auth handles user registration/login and current-user identity retrieval within tenant-aware access flows.

## Capability summary (route groups)
- Base route group: `/auth`
- High-level capabilities:
  - register
  - login
  - get current user (`/me`)

## Layer map
- API: `src/Intentify.Modules.Auth.Api`
- Application: `src/Intentify.Modules.Auth.Application`
- Domain: `src/Intentify.Modules.Auth.Domain`
- Infrastructure: `src/Intentify.Modules.Auth.Infrastructure`
- Tests: `tests/Intentify.Modules.Auth.Tests`

## Module-specific configuration
Verified module-specific bindings:
- `Intentify:Mongo` (bound to `MongoOptions` in Auth module registration)
- `Intentify:Jwt` (bound to `JwtOptions` in Auth module registration)

## Key integrations and dependencies
- Shared packages: `Intentify.Shared.Data.Mongo`, `Intentify.Shared.Security`, `Intentify.Shared.Web`
- Auth-specific endpoint filtering and identity helpers in API layer

## Change-location guide
- Route wiring: `src/Intentify.Modules.Auth.Api/AuthModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Auth.Api/*`
- Application commands/handlers/interfaces: `src/Intentify.Modules.Auth.Application/*`
- Domain entities/roles: `src/Intentify.Modules.Auth.Domain/*`
- Repositories: `src/Intentify.Modules.Auth.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Auth/tests/Intentify.Modules.Auth.Tests/Intentify.Modules.Auth.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Auth.Api/README.md`
- `src/Intentify.Modules.Auth.Application/README.md`
- `src/Intentify.Modules.Auth.Domain/README.md`
- `src/Intentify.Modules.Auth.Infrastructure/README.md`
- `tests/Intentify.Modules.Auth.Tests/README.md`
