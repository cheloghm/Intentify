# Intentify.Modules.Leads.Application

## Responsibility
- Defines Leads use-case contracts and orchestrates business workflows between API, domain models, and infrastructure abstractions.
- Keeps HTTP and persistence concerns outside this layer.

## Command/Query/Handler Map
- Contracts (`LeadContracts.cs`):
  - `UpsertLeadFromPromoEntryCommand`
  - `ListLeadsQuery`
  - `GetLeadQuery`
- Handlers (`Handlers.cs`):
  - `UpsertLeadFromPromoEntryHandler`
  - `ListLeadsHandler`
  - `GetLeadHandler`

## Contracts/Interfaces Map
- `ILeadRepository`: lead persistence abstraction for lookup, insert, replace, list.
- `ILeadVisitorLinker`: visitor resolution/enrichment abstraction used during lead upsert.

## Validation and Orchestration Points
- `UpsertLeadFromPromoEntryHandler` performs normalization/truncation for email, first-party id, and name.
- Upsert flow:
  - resolve existing lead (email, then first-party id),
  - resolve linked visitor,
  - insert or update lead,
  - optionally enrich linked visitor when consent is granted.
- `GetLeadHandler` maps missing records to `OperationResult.NotFound`.

## Configuration Options
- No module-specific options are read directly in this layer.
- Runtime dependencies are provided via interfaces and registered in API module composition.

## Where To Add Business Use-Cases Safely
- Add new command/query records and interfaces in `LeadContracts.cs`.
- Add handler implementations in `Handlers.cs` (or split into additional files if the layer grows).
- Keep persistence/integration details behind `ILeadRepository` and `ILeadVisitorLinker` and implement them in Infrastructure.

## Related Docs
- API layer: `../Intentify.Modules.Leads.Api/README.md`
- Domain layer: `../Intentify.Modules.Leads.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Leads.Infrastructure/README.md`
- Module root: `../../README.md`
