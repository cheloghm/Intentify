# Intentify.Modules.Tickets.Tests

## Test project scope
- Covers `Intentify.Modules.Tickets.Tests` behaviors through the test files in this folder.
- Keep assertions aligned with currently implemented behavior only; avoid undocumented assumptions.

## Test types used
- Integration tests.
- Test entry files currently present:
  - `TicketsIntegrationTests.cs`

## Important fixtures/helpers/dependencies
- `Microsoft.AspNetCore.TestHost` for in-memory host/testing pipeline scenarios
- `AssemblyInfo.cs` disables test parallelization to reduce cross-test interference
- Test framework packages are declared in the project file (`xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`).

## Solution-wide command(s)
- `dotnet test -c Debug Intentify.sln --no-build`
- `dotnet test -c Debug Intentify.sln` (if build step is needed)

## Project-specific command(s)
- `dotnet test -c Debug src/backend/modules/Intentify.Modules.Tickets/tests/Intentify.Modules.Tickets.Tests/Intentify.Modules.Tickets.Tests.csproj`
- `dotnet test -c Debug src/backend/modules/Intentify.Modules.Tickets/tests/Intentify.Modules.Tickets.Tests/Intentify.Modules.Tickets.Tests.csproj --filter FullyQualifiedName~Intentify.Modules.Tickets`

## Determinism / anti-flake notes
- Prefer deterministic inputs and explicit assertions over timing-sensitive checks.
- Avoid shared mutable state across tests; keep test data isolated per test case.
- For integration-style tests, keep filters narrow when debugging (`--filter`) and run repeatedly before merging.

## Related docs
- Related production README: `../../README.md`
- Testing playbook: `docs/codex/03-testing-playbook.md`
- Test project guardrails: `docs/codex/02-test-project-guardrails.md`
