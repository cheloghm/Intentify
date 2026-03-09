# Intentify.Modules.Auth.Application

## Application layer responsibility
Implements Auth use-case orchestration for user registration and login.

## Command/query/handler map
Contracts and commands:
- [`AuthCommands.cs`](AuthCommands.cs)
- [`AuthTokenResult.cs`](AuthTokenResult.cs)

Handlers:
- [`RegisterUserHandler.cs`](RegisterUserHandler.cs)
- [`LoginUserHandler.cs`](LoginUserHandler.cs)

## Contracts/interfaces map
Repository abstractions:
- [`IUserRepository.cs`](IUserRepository.cs)
- [`ITenantRepository.cs`](ITenantRepository.cs)

## Validation/orchestration points
- `RegisterUserHandler` validates display name/email/password and ensures tenant/user creation flow.
- `LoginUserHandler` validates credentials and token issuance flow.
- Validation errors and operation statuses are returned through shared validation result patterns.

## Configuration options used here if verified
- `JwtOptions` is consumed via `IOptions<JwtOptions>` for token issuance behavior.

## Where to add business use-cases safely
- Add new commands/contracts in [`AuthCommands.cs`](AuthCommands.cs) or dedicated contract files.
- Add new handlers alongside existing handler pattern.
- Keep API concerns in Api layer and persistence concerns behind repository interfaces.

## Related docs
- API layer: [`../Intentify.Modules.Auth.Api/README.md`](../Intentify.Modules.Auth.Api/README.md)
- Domain layer: [`../Intentify.Modules.Auth.Domain/README.md`](../Intentify.Modules.Auth.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Auth.Infrastructure/README.md`](../Intentify.Modules.Auth.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
