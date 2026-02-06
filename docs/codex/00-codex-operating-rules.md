# Codex Operating Rules (Mandatory)

## 1) Core Principle
Do exactly what the task asks—nothing more.

## 2) Smallest Possible Diff
- Prefer minimal edits over refactors.
- Never rename or move folders unless the task explicitly requests it.
- Never “clean up” unrelated code.

## 3) DRY (Do Not Repeat Yourself)
Before writing code, search the repo for existing equivalents:
- options/config binding
- DTOs/Contracts
- validators
- error/result primitives
- middleware (correlation-id, ProblemDetails)
- Mongo helpers and index patterns
- logging/observability setup

If it already exists, reuse it. Do not create duplicates.

## 4) DevSecOps Defaults
- No secrets in code or committed files.
- Use environment variables or .env (local only) and .env.example (safe placeholders).
- Never log secrets (JWT keys, API keys, connection strings, tokens).
- Use least privilege patterns (scoped DI, minimal exposure of endpoints).
- Dev-only endpoints must be gated by environment and sanitized.

## 5) Do Not Break Contracts
- Do not change public API routes, request/response DTO shapes, or auth/authorization behavior unless explicitly requested.

## 6) Stop-and-Ask Rule (Mandatory)
If any of these are unclear or risky, STOP and ask the user:
- Auth flows (JWT claims/policies)
- Multi-tenancy rules
- Public API contracts (DTOs, routes)
- Security requirements
- DB migrations/data shape changes
- External integrations (URLs, keys, rate limits)

## 7) Output Requirements per Task
When you propose changes, always include:
- Scope: what you will change / will not change / files touched
- Assumptions: verified vs unknown
- Definition of Done: pass/fail gates

After changes:
- exact commands to run
- expected output/behavior
- 3–8 manual checks
- rollback steps

You are working on an EXISTING codebase. Do NOT re-scaffold, regenerate, or replace files unless explicitly asked.

Before making changes:
1) Sync and verify baseline:
   - git status (must be clean)
   - git pull (or confirm branch up-to-date)
   - dotnet test -c Debug .\Intentify.sln (capture failing projects + exact errors)

2) Analyze before editing:
   - Identify existing conventions (central packages via Directory.Packages.props if present).
   - Reuse existing patterns and shared packages; do not reimplement helpers already present.
   - If you need xUnit Facts/IAsyncLifetime, verify the test csproj already references:
     Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio.
   - If you use ServiceCollection / BuildServiceProvider / GetRequiredService / GetServices,
     ensure the project references Microsoft.Extensions.DependencyInjection and includes
     `using Microsoft.Extensions.DependencyInjection;`.

Change rules:
- Smallest possible diff.
- Only edit files required to fix the stated errors.
- Do not delete or replace files to “make it work”.
- Do not add new libraries unless already used in repo; prefer project/package references consistent with current repo.

Commit rules:
- After each fix, rerun:
  dotnet restore .\Intentify.sln
  dotnet build  -c Debug .\Intentify.sln
  dotnet test   -c Debug .\Intentify.sln
- If any command fails, STOP and fix before committing.
- Include in PR description: list of files changed + test output summary.

If my previous PR conflicts with local fixes, rebase/merge from main first and KEEP the local fixes.
Do not overwrite changes made outside this PR scope.
