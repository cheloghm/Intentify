# Intentify — Claude Context File

## What Is Intentify?
Intentify is a B2B micro-SaaS platform that combines **website visitor identification** with
**search intent intelligence** — think Retention.com + Captify in one product.

Its core value proposition:
- Capture anonymous website visitors and enrich them into leads
- Overlay those leads with real-time search intent data (what people are actively searching for)
- Help clients act on that data via AI chat, flows, ads, and notifications

**Current version:** V1 — Google-only intelligence (Google Ads + Google Trends as data sources)

---

## Architecture Overview

Intentify is a **modular monolith** built with:
- **Backend:** .NET (C#) — ASP.NET Core, organized as vertical modules inside one solution
- **Frontend:** JavaScript/React (Vite), located in `src/frontend/web/`
- **Database:** MongoDB (via `Intentify.Shared.Data.Mongo`)
- **Messaging:** Internal messaging system (via `Intentify.Shared.Messaging`)
- **Observability:** Shared observability (via `Intentify.Shared.Observability`)
- **Containerization:** Docker + Docker Compose
- **Orchestration (dev):** .NET Aspire (`src/backend/app/Intentify.AppHost`)
- **Testing:** xUnit + Testcontainers (spins up real Docker containers per test run)
- **Package management:** Central package versioning via `Directory.Packages.props`
- **Build props:** Shared build config via `Directory.Build.props`

---

## Repository Structure

```
Intentify/
├── .github/                        # GitHub Actions CI workflows
├── docs/                           # Architecture and product docs
├── src/
│   ├── backend/
│   │   ├── app/
│   │   │   └── Intentify.AppHost/  # .NET Aspire orchestration (dev runner)
│   │   ├── modules/                # Feature modules (one per business domain)
│   │   │   ├── Intentify.Modules.Ads/
│   │   │   ├── Intentify.Modules.Auth/
│   │   │   ├── Intentify.Modules.Collector/
│   │   │   ├── Intentify.Modules.Engage/
│   │   │   ├── Intentify.Modules.Flows/
│   │   │   ├── Intentify.Modules.Intelligence/
│   │   │   ├── Intentify.Modules.Knowledge/
│   │   │   ├── Intentify.Modules.Leads/
│   │   │   ├── Intentify.Modules.PlatformAdmin/
│   │   │   ├── Intentify.Modules.Promos/
│   │   │   ├── Intentify.Modules.Sites/
│   │   │   ├── Intentify.Modules.Tickets/
│   │   │   └── Intentify.Modules.Visitors/
│   │   ├── shared/                 # Cross-cutting shared libraries
│   │   │   ├── Intentify.Shared.Abstractions/
│   │   │   ├── Intentify.Shared.AI/
│   │   │   ├── Intentify.Shared.Data.Mongo/
│   │   │   ├── Intentify.Shared.KeyManagement/
│   │   │   ├── Intentify.Shared.Messaging/
│   │   │   ├── Intentify.Shared.Observability/
│   │   │   ├── Intentify.Shared.Security/
│   │   │   ├── Intentify.Shared.Testing/
│   │   │   ├── Intentify.Shared.Validation/
│   │   │   └── Intentify.Shared.Web/
│   │   ├── Intentify.sln           # Main .NET solution file
│   │   ├── Directory.Build.props   # Shared MSBuild configuration
│   │   └── Directory.Packages.props # Central NuGet package versioning
│   └── frontend/
│       └── web/                    # React/Vite frontend
│           ├── src/                # App source code
│           ├── public/             # Static assets
│           ├── scripts/            # Build/deploy scripts
│           └── docker/             # Frontend Docker config
├── docker-compose.yml              # Full stack local environment
├── .env.compose                    # Env vars for docker-compose
└── .env.example                    # Env var template
```

---

## Backend Modules — What Each One Does

### `Intentify.Modules.Auth`
User registration, login, JWT issuance/validation, tenant membership, roles & permissions, session lifecycle.
All other modules rely on this for authentication.

### `Intentify.Modules.Sites`
Website/domain registration for tenants. Generates site keys and widget keys. Enforces allowed origins
(CORS-style). Tracks installation verification (first event received). Supports dev/prod environment separation.

### `Intentify.Modules.Collector`
Serves `tracker.js` as a static file. Accepts pageview and event payloads from client websites.
Validates site key, origin, and payload size. Applies rate limiting. Emits "first event received" signal.
This is the data ingestion entry point.

### `Intentify.Modules.Visitors`
Anonymous visitor identification. Session creation and tracking. Visit frequency (7/30/90 day windows).
Page timelines. Device and coarse geo info. Links visitors to leads when identified.

### `Intentify.Modules.Leads`
Lead creation (from chat, promo, or forms). Links leads to visitor records. Consent logging.
Lead profile and history. Basic tagging and status management.

### `Intentify.Modules.Knowledge`
AI grounding and memory workspace (Eden-style). Manages knowledge sources: website URLs, PDFs, text notes.
Runs a content ingestion pipeline: fetch → extract → chunk → index. Tracks index refresh status.
Exposes retrieval interface for AI. **Critical rule: AI responses must only use this module as knowledge source.**

### `Intentify.Modules.Engage`
SiteGPT-style AI chat widget. Handles chat widget bootstrap (public endpoint). Persists chat sessions
across pages. Delivers knowledge-grounded responses. Scores confidence. Escalates to Tickets when uncertain.
Supports widget theming to match client site.

### `Intentify.Modules.Tickets`
Human handoff system. Creates tickets from AI escalation or manually. Links tickets to visitor, lead,
and chat. Status lifecycle: open → in-progress → closed. Internal notes. Feeds back to Knowledge Workspace.

### `Intentify.Modules.Intelligence`
The Captify-style core — market and audience intelligence. Integrates with Google Ads and Google Trends.
Provides search intent data, keyword trends, and audience signals. Runs recurring refresh workers.
**Note: Several integration tests currently failing due to Google API configuration issues.**

### `Intentify.Modules.Flows`
Automation engine. Triggers actions based on visitor/lead behavior or intelligence signals.
Supports flow creation, run tracking, and multi-step workflow execution.

### `Intentify.Modules.Ads`
Ad audience management. Helps clients act on search intent data by creating targeted ad audiences.

### `Intentify.Modules.Promos`
Promotional campaigns tied to visitor capture and lead conversion.

### `Intentify.Modules.PlatformAdmin`
Internal platform administration. Oversight and management tools for the Intentify team.

---

## Shared Libraries — What Each One Does

| Library | Purpose |
|---|---|
| `Shared.Abstractions` | Base interfaces, domain primitives, common contracts |
| `Shared.AI` | AI/LLM integration helpers (used by Engage and Knowledge) |
| `Shared.Data.Mongo` | MongoDB data access layer, repository patterns |
| `Shared.KeyManagement` | API key generation, site key and widget key management |
| `Shared.Messaging` | Internal in-process messaging (events between modules) |
| `Shared.Observability` | Logging, tracing, metrics (structured via ILogger) |
| `Shared.Security` | Auth middleware, JWT validation helpers, security policies |
| `Shared.Testing` | Testcontainers base classes, integration test helpers, fixtures |
| `Shared.Validation` | Input validation helpers and FluentValidation integration |
| `Shared.Web` | ASP.NET Core middleware, endpoint helpers, HTTP utilities |

---

## Known Issues (as of last test run)

### Intelligence Module — 9 failing tests
Tests in `Intentify.Modules.Intelligence.Tests` are failing with `BadRequest` responses where `OK` is expected.
Root causes:
1. `GoogleAdsHistoricalMetricsProviderTests` — `ObjectDisposedException` on `HttpContent` (disposed before async read)
2. `GoogleTrendsIntegrationTests` and `IntelligenceIntegrationTests` — returning `BadRequest` instead of `OK`,
   likely due to missing or misconfigured Google API credentials in the test environment.

**Do not touch the migrations folder or the test fixtures in `Shared.Testing` without explicit instruction.**

---

## API Conventions (observed from logs)

- REST API, JSON payloads
- Routes follow: `POST /auth/register`, `POST /sites/`, `POST /collector/events`, etc.
- Responses use standard HTTP status codes (200, 400, etc.)
- Multi-tenant: all scoped operations are tenant-isolated
- Site operations are scoped by `siteId` (e.g. `/sites/{siteId}/origins`)

---

## Development Environment

- Run locally via **Docker Compose**: `docker-compose up` from root
- Alternatively use **.NET Aspire** (`Intentify.AppHost`) for orchestrated local dev
- Backend `.env` lives at `src/backend/.env` (do not commit secrets)
- Root `.env.compose` is for docker-compose overrides
- Use `.env.example` as the reference template for required variables

### Running Tests
```bash
cd src/backend
dotnet test
```
Tests use **Testcontainers** — they spin up real Docker containers, so Docker must be running.
Test runs are slow (~141s for Collector, ~354s for Intelligence).

---

## Coding Conventions

### Backend (C# / .NET)
- Vertical slice architecture per module — each module is self-contained
- Minimal APIs style (no heavy MVC controllers — uses endpoint classes like `CreateSiteAsync`)
- Async/await everywhere — no blocking `.Result` or `.Wait()` calls
- Central package management — add new NuGet packages via `Directory.Packages.props`, not per-project
- Shared build settings via `Directory.Build.props` — do not duplicate props in individual `.csproj` files
- Use `Shared.*` libraries for cross-cutting concerns — never duplicate logging, validation, or data access logic
- Tests live inside each module under a `tests/` subfolder

### Frontend (React / Vite)
- Located at `src/frontend/web/src/`
- Standard React conventions apply
- `env-config.js` handles runtime environment config injection (not baked into the build)

---

## What NOT to Touch

- `src/backend/.env` — contains real secrets, never log or expose its values
- `.env.compose` — docker-compose secrets
- `/migrations` — never edit migration files directly, always generate via tooling
- `Directory.Packages.props` — only update when intentionally adding/upgrading a NuGet package
- `Directory.Build.props` — only update for intentional build config changes
- Test fixture base classes in `Intentify.Shared.Testing` — unless fixing a known testing infrastructure issue

---

## Key Business Rules

1. **Multi-tenancy is sacred** — every data query must be scoped to a tenant. Never return cross-tenant data.
2. **AI must only use the Knowledge Workspace** — Engage module responses must be grounded exclusively
   in the tenant's Knowledge sources. No hallucinated or external data in AI responses.
3. **Consent before identity** — Leads must have consent logged before being used for outreach.
4. **Site key validation** — the Collector must always validate site key + origin before accepting events.
5. **Intelligence is V1 Google-only** — do not add non-Google data providers without explicit instruction.

## Development Workflow
- Never run git add, git commit, or git push unless explicitly 
  instructed to do so by the user
- Never suggest committing as part of completing a task
- The user handles all version control
