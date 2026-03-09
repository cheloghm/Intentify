# Intentify.Shared.Testing

## Package purpose and scope
Provides shared test infrastructure utilities used by backend test projects.

## Key public types/classes
- `MongoContainerFixture`

## Dependencies and boundary notes
- Test-only support package.
- Production/runtime projects should not depend on this package.

## Known consumers (easy-to-verify)
- Backend test projects requiring shared Mongo container fixture behavior.

## Package-specific configuration
No package-owned runtime configuration.

## How to extend safely
- Keep helpers deterministic and suitable for repeatable CI/test execution.
- Prefer reusable fixtures/helpers over module-specific assertions.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Testing/tests/Intentify.Shared.Testing.Tests/Intentify.Shared.Testing.Tests.csproj
```

## Related codex references
- `docs/codex/03-testing-playbook.md`
- `docs/codex/02-test-project-guardrails.md`
