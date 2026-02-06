# PR Template (Use in every Codex PR)

## What changed
- (bullet list)

## What did NOT change
- (bullet list)

## Files touched
- (list)

## How to test
- Commands:
  - dotnet build -c Debug
  - dotnet test -c Debug
  - (frontend commands if relevant)

## Manual checks (3–8)
- (list)

## Security notes
- Secrets handling:
- Dev-only endpoint guards:
- Any new exposure surface:

## Rollback
- git revert <commit>
