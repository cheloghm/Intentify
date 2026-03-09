# Intentify.Modules.Tickets.Api

## API layer responsibility
- Exposes Tickets HTTP endpoints and wires module services in `TicketsModule`.
- Maps HTTP request DTOs to application commands/queries and maps operation results to HTTP responses.

## Route groups exposed
- Authenticated route group (`RequireAuthFilter`) under `/tickets`:
  - `POST /tickets`
  - `GET /tickets`
  - `GET /tickets/{ticketId}`
  - `PUT /tickets/{ticketId}`
  - `PUT /tickets/{ticketId}/assignment`
  - `POST /tickets/{ticketId}/notes`
  - `GET /tickets/{ticketId}/notes`
  - `PUT /tickets/{ticketId}/status`

Route mapping source of truth: `TicketsModule.cs` and `TicketsEndpoints.cs`.

## Request/response model location
- API request models are in `TicketsModels.cs`.
- Endpoint parsing/result mapping lives in `TicketsEndpoints.cs`.
- Application contracts used by endpoints are in `../Intentify.Modules.Tickets.Application/TicketContracts.cs`.

## Auth/authorization requirements
- Entire `/tickets` group applies `RequireAuthFilter`.
- Endpoints additionally require a valid `tenantId` claim and return `401` when unavailable.

## Error/result mapping conventions
- Invalid route/query GUID inputs return `400` validation ProblemDetails.
- Validation failures from handlers map to `400`.
- Not-found handler results map to `404`.
- Success responses map to `200`.
- Pagination normalization is handled in endpoints (`page` default 1, `pageSize` bounded to max 200).

## Where to add/change endpoints
- Register new routes in `TicketsModule.cs`.
- Add binding + HTTP mapping logic in `TicketsEndpoints.cs`.
- Add/extend request models in `TicketsModels.cs`.
- Add corresponding application contracts/handlers in `../Intentify.Modules.Tickets.Application` before endpoint wiring.

## Related docs
- Application layer: `../Intentify.Modules.Tickets.Application/README.md`
- Domain layer: `../Intentify.Modules.Tickets.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Tickets.Infrastructure/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
