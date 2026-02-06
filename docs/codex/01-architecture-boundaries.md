# Architecture Boundaries (Mandatory)

This repo uses modules + shared packages. These boundaries prevent circular dependencies and fragile coupling.

## Within a module
- **Api** → can depend on **Application + Contracts**
- **Application** → can depend on **Domain + Contracts + Shared.Abstractions**
- **Domain** → depends on **nothing else** (except tiny universal primitives IF they already exist)
- **Infrastructure** → can depend on **Application + Domain + Shared infra** (Mongo/Observability/Security), but:
  - **Infrastructure must not be referenced by other modules**
  - Infrastructure is private to its module

## Cross-module calls
- Allowed:
  - via **Contracts** (DTOs/events/interfaces)
  - via **Shared.Messaging** (in-process messaging abstraction)
- Not allowed:
  - referencing another module’s Infrastructure
  - directly querying another module’s DB collections unless explicitly approved

## Where features belong (to avoid “wrong implementations”)
- Visitor analytics/visit counts → Visitors + Collector + Trends
- Visitor profile composition → ProfileAggregation (reads composed view fed by events)
- SEO/content ideas → dedicated Growth/Content module (not Engage)
- Social scraping/enrichment → separate Enrichment/SocialInsights module with compliance boundaries
- Chat across pages → Engage conversation/session model + widget frontend package
- Promos/draw signups → Leads capture + Flows trigger/action engine + Messaging notify
- Admin dashboard subscriber tracking → Admin module + admin UI routes
- Snippet endpoint / tracker.js → AppHost + Collector public script surface (not dashboard paths)
