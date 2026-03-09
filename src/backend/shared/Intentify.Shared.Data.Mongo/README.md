# Intentify.Shared.Data.Mongo

## Package purpose and scope
Provides shared MongoDB setup primitives used by modules for connection validation, conventions, and index helper behavior.

## Key public types/classes
- `MongoOptions`
- `MongoClientFactory`
- `MongoConventions`
- `MongoIndexHelper`

## Dependencies and boundary notes
- Shared infrastructure package for Mongo-specific behavior.
- Modules should keep domain logic outside this package and use this package only for Mongo plumbing.

## Known consumers (easy-to-verify)
- `Intentify.Modules.Auth` binds `MongoOptions` and uses `MongoClientFactory` / `MongoConventions` during service registration.

## Package-specific configuration
Common section used by consumers:
- `Intentify:Mongo` (mapped to `MongoOptions`)

## How to extend safely
- Keep additions Mongo-focused and reusable across modules.
- Avoid embedding module-specific collection semantics.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Data.Mongo/tests/Intentify.Shared.Data.Mongo.Tests/Intentify.Shared.Data.Mongo.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/03-testing-playbook.md`
