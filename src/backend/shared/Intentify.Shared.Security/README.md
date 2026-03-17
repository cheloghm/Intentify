# Intentify.Shared.Security

## Package purpose and scope
Provides shared security primitives for JWT issuance/validation and password hashing.

## Key public types/classes
- `JwtOptions`
- `JwtTokenIssuer`
- `JwtTokenValidator`
- `PasswordHasher`

## Dependencies and boundary notes
- Shared package for cross-module security utilities.
- Keep authorization policy decisions in host/modules; keep cryptographic/token helpers here.

## Known consumers (easy-to-verify)
- `Intentify.Modules.Auth` registers and uses security services from this package.
- AppHost uses JWT-related configuration keys compatible with `JwtOptions` usage in modules.

## Package-specific configuration
Common section used by consumers:
- `Intentify:Jwt` (mapped to `JwtOptions`)

## How to extend safely
- Maintain backward-compatible token/password behavior unless a coordinated migration is planned.
- Keep API surface explicit and test-covered for security-sensitive changes.

## Tests and commands
From `src/backend`:

```bash
dotnet test shared/Intentify.Shared.Security/tests/Intentify.Shared.Security.Tests/Intentify.Shared.Security.Tests.csproj
```

## Related codex references
- `docs/codex/01-architecture-boundaries.md`
- `docs/codex/03-testing-playbook.md`
