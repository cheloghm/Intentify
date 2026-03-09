# Intentify.Modules.Auth.Domain

## Domain layer responsibility
Defines Auth domain entities and constants for users, tenants, roles, and collection naming.

## Entities/value objects/enums map
- User entity: [`User.cs`](User.cs)
- Tenant entity: [`Tenant.cs`](Tenant.cs)
- Role constants: [`AuthRoles.cs`](AuthRoles.cs)
- Collection name constants: [`AuthMongoCollections.cs`](AuthMongoCollections.cs)

## Invariants/business rules
- Role names are centrally defined in `AuthRoles`.
- Core field shape for tenant/user identity is defined in domain entities.
- Input validation/business workflow rules are primarily enforced in Application handlers.

## Persistence-agnostic constraints
- Domain entities stay free of API endpoint concerns.
- Domain project focuses on model shape and shared constants.

## Where to change business model safely
- Change user/tenant fields in [`User.cs`](User.cs) and [`Tenant.cs`](Tenant.cs).
- Keep compatibility with Application contracts and Infrastructure repository mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Auth.Application/README.md`](../Intentify.Modules.Auth.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
