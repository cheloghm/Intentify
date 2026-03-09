# Intentify.Modules.Sites.Application

## Application layer responsibility
- Defines Sites use-case contracts and orchestrates site lifecycle, key management, and installation-status checks.
- Keeps persistence and key-generation implementation details behind abstractions.

## Command/query/handler map
- Contracts (`SiteCommands.cs`):
  - `CreateSiteCommand`
  - `UpdateAllowedOriginsCommand`
  - `RotateKeysCommand`
  - `GetSiteKeysCommand`
  - `GetInstallationStatusCommand`
  - `GetPublicInstallationStatusCommand`
  - `PublicInstallationStatusResult`
- Handlers:
  - `CreateSiteHandler`
  - `ListSitesHandler`
  - `UpdateAllowedOriginsHandler`
  - `RotateKeysHandler`
  - `GetSiteKeysHandler`
  - `GetInstallationStatusHandler`
  - `GetPublicInstallationStatusHandler`

## Contracts/interfaces map
- `ISiteRepository`: site lookup/list/create/update operations (tenant, keys, origins, installation timestamps).
- `IKeyGenerator` (from `Intentify.Shared.KeyManagement`): generates site and widget keys used in create/rotate flows.

## Validation/orchestration points
- `CreateSiteHandler` validates/normalizes domain input, checks tenant-domain uniqueness, generates keys, and inserts site records.
- `UpdateAllowedOriginsHandler` normalizes origin lists and updates site origin policy.
- `RotateKeysHandler` regenerates and persists both site and widget keys.
- Installation-status handlers compose repository lookups and expose tenant-scoped or public status outcomes.

## Configuration options used here if verified
- No module-specific configuration options are read directly in this layer.
- Behavior is driven by injected `ISiteRepository` and `IKeyGenerator` services.

## Where to add business use-cases safely
- Add new commands/queries/results in `SiteCommands.cs` (or adjacent contract files if the set grows).
- Add new handlers in this layer and keep transport/persistence details out of handler logic.
- Extend `ISiteRepository` only when new persistence capabilities are required, then implement in Infrastructure.

## Related docs
- API layer: `../Intentify.Modules.Sites.Api/README.md`
- Domain layer: `../Intentify.Modules.Sites.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Sites.Infrastructure/README.md`
- Module root: `../../README.md`
