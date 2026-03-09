# Intentify.Modules.Collector.Api

## API layer responsibility
Defines Collector HTTP route wiring and endpoint handlers for tracker script delivery and event ingestion.

## Route groups exposed
- Base route group: `/collector`
- High-level endpoints:
  - `/tracker.js`
  - `/events`

Stable route mapping reference: [`CollectorModule.cs`](CollectorModule.cs).

## Request/response model location
- API transport models: [`CollectorModels.cs`](CollectorModels.cs)
- Endpoint handler methods: [`CollectorEndpoints.cs`](CollectorEndpoints.cs)

## Auth/authorization requirements
- Collector endpoints are public at API layer.
- Ingestion authorization is enforced by handler logic through site key lookup and allowed-origin checks (application layer), not by `RequireAuthFilter`.

## Error/result mapping conventions
`CollectorEndpoints` maps request/handler outcomes to HTTP results, including:
- payload too large -> `413 Payload Too Large`
- validation failures -> `400 Bad Request` with problem details
- not found site key -> `404 Not Found`
- forbidden origin -> `403 Forbidden`
- success -> `200 OK`

## Where to add/change endpoints
- Route wiring changes: [`CollectorModule.cs`](CollectorModule.cs)
- Endpoint logic: [`CollectorEndpoints.cs`](CollectorEndpoints.cs)
- Request model changes: [`CollectorModels.cs`](CollectorModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Collector.Application/README.md`](../Intentify.Modules.Collector.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
