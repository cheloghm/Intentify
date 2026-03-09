# Intentify.Modules.Leads.Api

## Responsibility
- Hosts the Leads module HTTP surface and wires the module into the app host via `LeadsModule`.
- Registers the module's application handlers and infrastructure adapters in DI.

## Route Groups Exposed
- Base group: `/leads` in `LeadsModule.cs`.
- Endpoints currently mapped:
  - `GET /leads`
  - `GET /leads/{leadId}`

Keep route details summarized here; use `LeadsModule.cs` and `LeadsEndpoints.cs` as source of truth.

## Request/Response Model Location
- Querystring/path input binding is implemented directly in endpoint method signatures in `LeadsEndpoints.cs`.
- Query contracts are defined in `../Intentify.Modules.Leads.Application/LeadContracts.cs`.
- Response payloads are domain `Lead` objects from `../Intentify.Modules.Leads.Domain/Lead.cs`.

## Auth/Authorization
- All `/leads` endpoints are protected by `RequireAuthFilter` applied at route-group level in `LeadsModule.cs`.
- Endpoint handlers additionally require a valid `tenantId` claim and return `401` when unavailable.

## Error/Result Mapping Conventions
- Invalid identifiers (for example `siteId`, `leadId`) return `400` validation ProblemDetails via `ProblemDetailsHelpers.CreateValidationProblemDetails(...)`.
- Missing tenant context returns `401 Unauthorized`.
- `Get` maps `OperationResult.NotFound` to `404` and success to `200`.
- `List` returns `200` with paged collection results.

## Where To Add/Change Endpoints
- Add route mappings in `LeadsModule.cs`.
- Implement endpoint logic in `LeadsEndpoints.cs` and delegate business logic to application handlers.
- Prefer adding new command/query contracts in `../Intentify.Modules.Leads.Application/LeadContracts.cs` before adding endpoint behavior.

## Related Docs
- Application layer: `../Intentify.Modules.Leads.Application/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
