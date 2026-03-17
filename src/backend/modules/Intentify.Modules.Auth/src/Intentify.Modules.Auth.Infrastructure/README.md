# Intentify.Modules.Auth.Infrastructure

## Infrastructure layer responsibility
Implements Auth persistence adapters for users and tenants.

## Repositories/adapters map
- User repository: [`UserRepository.cs`](UserRepository.cs)
- Tenant repository: [`TenantRepository.cs`](TenantRepository.cs)

## Storage/external integration details
- Uses MongoDB collections defined by `AuthMongoCollections` (`auth.users`, `auth.tenants`).
- Uses shared Mongo helper utilities (`MongoIndexHelper`) for index creation.

## Config/options consumed in this layer
No Auth-specific options type is defined in this layer.
Runtime storage behavior depends on injected `IMongoDatabase` and module-level `Intentify:Mongo` binding.

## Failure/operational notes
- Repositories ensure indexes before query/insert operations.
- Mongo operation failures are not translated in this layer; they bubble to callers.

## Where to add persistence/integration changes safely
- Extend repository behavior in [`UserRepository.cs`](UserRepository.cs) or [`TenantRepository.cs`](TenantRepository.cs).
- Keep interface compatibility with Application abstractions (`IUserRepository`, `ITenantRepository`).

## Related docs
- Application layer: [`../Intentify.Modules.Auth.Application/README.md`](../Intentify.Modules.Auth.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
