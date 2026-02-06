# Dependency boundaries (hard rules)

Within a module:
- Api -> Application + Contracts
- Application -> Domain + Contracts + Shared abstractions
- Domain -> nothing else (except tiny universal primitives)
- Infrastructure -> Application + Domain + Shared infra
- No other project may reference Infrastructure.

Cross-module calls:
- Only via Contracts (DTOs/events/interfaces) OR in-process messaging abstraction (Shared.Messaging).

Critical:
- No module references another module’s Infrastructure.
- No circular project references.
