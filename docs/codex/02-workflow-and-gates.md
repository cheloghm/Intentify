# Workflow and Gates (Mandatory)

## Golden Rule: One Step at a Time
Do not start the next package/module until this step is green:
1) Build succeeds
2) Tests pass
3) Manual smoke checks pass

## Standard Implementation Order (Backend)
For each module:
1) Domain
2) Application
3) Infrastructure
4) Api
5) Tests for each layer as needed (unit first, then integration)

## Standard Implementation Order (Frontend)
For each module UI:
1) Types/contracts
2) API client call
3) UI screen/component
4) Smoke test/manual validation

## Minimum Gate Commands
Backend:
- dotnet build -c Debug
- dotnet test -c Debug

Frontend (if changed):
- npm ci
- npm run build

## Commit Discipline
- Commit after each “green gate” checkpoint.
- If the next step breaks, revert to last green commit.
