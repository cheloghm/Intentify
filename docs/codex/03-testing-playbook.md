# Testing Playbook (Mandatory)

## Test Pyramid
- Prefer unit tests for pure logic (Domain/Application).
- Use integration tests only where needed (Mongo, middleware, endpoint wiring).

## Deterministic Tests
- No external network calls in unit tests.
- AI integrations must be mockable; tests must not call real providers.

## What Every Module Must Prove
- Core logic works (unit tests)
- DI wiring works (integration/smoke)
- API endpoints return expected shapes and status codes
- Auth/tenant rules enforced where applicable

## Minimum Required Outputs After Changes
- Provide the exact test commands run
- Provide the full failing output if any test fails
