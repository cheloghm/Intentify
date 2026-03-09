# Intentify.Modules.Leads.Infrastructure

## Responsibility
- Implements application-layer abstractions for lead persistence and visitor-link integration.
- Encapsulates MongoDB access patterns for the Leads module.

## Repositories / Adapters Map
- `LeadRepository` (`LeadRepository.cs`) implements `ILeadRepository` with:
  - lookup by email/first-party id/id,
  - insert/replace,
  - paged list by tenant (optionally by site), sorted by `UpdatedAtUtc`.
- `LeadVisitorLinker` (`LeadVisitorLinker.cs`) implements `ILeadVisitorLinker` by resolving visitor identity and optional profile enrichment.

## Storage and External Integration Details
- Leads are stored in Mongo collection name from `LeadsMongoCollections.Leads`.
- Visitor linking reads/writes Visitors module records via `VisitorsMongoCollections.Visitors`.
- Repository index creation is centralized through `MongoIndexHelper.EnsureIndexesAsync(...)`.

## Config / Options Consumed
- Both adapters consume `IMongoDatabase` via DI.
- No Leads-specific options object is read in this layer.

## Failure / Operational Notes
- Repository methods await one-time async index initialization before queries/writes.
- Visitor enrichment is best-effort and gated by consent + visitor existence; when prerequisites are missing, the method exits without changes.

## Where To Add Persistence/Integration Changes Safely
- Extend lead storage/query behavior in `LeadRepository.cs` while preserving `ILeadRepository` contracts.
- Extend visitor resolution/enrichment behavior in `LeadVisitorLinker.cs` while preserving `ILeadVisitorLinker` contracts.
- If schema/index patterns change, coordinate updates with domain constants and application usage.

## Related Docs
- Application layer: `../Intentify.Modules.Leads.Application/README.md`
- Domain layer: `../Intentify.Modules.Leads.Domain/README.md`
- Module root: `../../README.md`
