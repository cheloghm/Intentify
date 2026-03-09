# Intentify.Shared.Validation

## Package purpose and scope
Provides shared validation primitives and normalization helpers used across modules.

## Key public types/classes
- `OperationResult<T>`
- `ValidationErrors`
- `Guard`
- `DomainNormalizer`
- `OriginNormalizer`

## Dependencies and boundary notes
- Shared package for validation/result primitives that can be reused by module application layers.
- Keep it generic; avoid module-specific validation rules in this package.

## Known consumers (easy-to-verify)
- Module application/API layers that return operation-status and validation error results.

## Package-specific configuration
None.

## How to extend safely
- Keep error/result semantics consistent (`OperationResult` status behavior).
- Add normalization helpers only when broadly reusable.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Validation/tests/Intentify.Shared.Validation.Tests/Intentify.Shared.Validation.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/03-testing-playbook.md`
