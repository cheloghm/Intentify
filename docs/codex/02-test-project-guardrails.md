# Test project guardrails (do not repeat prior failures)

## 1) Central Package Management (CPM) rule
If the repo uses Directory.Packages.props:
- NEVER put Version="" inside <PackageReference ...>.
- Versions must live in Directory.Packages.props via <PackageVersion Include="..." Version="..."/>.

## 2) xUnit rule (Fact/IAsyncLifetime)
Every xUnit test project MUST:
- Reference:
  - Microsoft.NET.Test.Sdk
  - xunit
  - xunit.runner.visualstudio
- Have at least one of:
  - `using Xunit;`
  - OR fully-qualified attributes (not recommended).

If a test uses:
- [Fact] => `using Xunit;` must exist (or ImplicitUsings must be enabled + global using exists).
- IAsyncLifetime => `using Xunit;` must exist.

## 3) DI extension methods rule (GetRequiredService / BuildServiceProvider)
If test code uses:
- ServiceCollection / BuildServiceProvider / GetRequiredService / GetServices
Then the test project MUST:
- Include `using Microsoft.Extensions.DependencyInjection;`
AND
- Have the correct package/framework reference that provides those extensions in THIS repo.

Do NOT “work around” by rewriting tests.
Fix the reference/usings so extension methods resolve.

## 4) No hidden failures
A green test summary is required.
If compilation errors exist in any project, fix them immediately even if some tests still ran.
