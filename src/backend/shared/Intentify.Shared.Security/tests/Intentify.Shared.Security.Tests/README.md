# Intentify.Shared.Security.Tests

## Test project scope
- Covers `Intentify.Shared.Security.Tests` behaviors through the test files in this folder.
- Keep assertions aligned with currently implemented behavior only; avoid undocumented assumptions.

## Test types used
- Unit/component tests.
- Test entry files currently present:
  - `SecurityTests.cs`

## Important fixtures/helpers/dependencies
- No custom fixture file is present in this folder; tests run with standard xUnit + project references.
- Test framework packages are declared in the project file (`xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`).

## Solution-wide command(s)
- `dotnet test -c Debug Intentify.sln --no-build`
- `dotnet test -c Debug Intentify.sln` (if build step is needed)

## Project-specific command(s)
- `dotnet test -c Debug src/backend/shared/Intentify.Shared.Security/tests/Intentify.Shared.Security.Tests/Intentify.Shared.Security.Tests.csproj`
- `dotnet test -c Debug src/backend/shared/Intentify.Shared.Security/tests/Intentify.Shared.Security.Tests/Intentify.Shared.Security.Tests.csproj --filter FullyQualifiedName~Intentify.Shared.Security`

## Determinism / anti-flake notes
- Prefer deterministic inputs and explicit assertions over timing-sensitive checks.
- Avoid shared mutable state across tests; keep test data isolated per test case.
- For integration-style tests, keep filters narrow when debugging (`--filter`) and run repeatedly before merging.

## Related docs
- Related production README: `../../README.md`
- Testing playbook: [`docs/codex/03-testing-playbook.md`](/docs/codex/03-testing-playbook.md)
- Test project guardrails: [`docs/codex/02-test-project-guardrails.md`](/docs/codex/02-test-project-guardrails.md)
