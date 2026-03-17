# Intentify.Modules.Engage

## Module purpose and bounded context
Engage manages chat/widget interactions, bot configuration, conversation history, and handoff orchestration.

## Capability summary (route groups)
- Base route group: `/engage`
- High-level capabilities:
  - public widget/bootstrap/chat endpoints
  - authenticated bot configuration endpoints
  - authenticated conversation/message retrieval endpoints

## Layer map
- API: `src/Intentify.Modules.Engage.Api`
- Application: `src/Intentify.Modules.Engage.Application`
- Domain: `src/Intentify.Modules.Engage.Domain`
- Infrastructure: `src/Intentify.Modules.Engage.Infrastructure`
- Tests: `tests/Intentify.Modules.Engage.Tests`

## Module-specific configuration
Verified module-specific keys in API registration:
- `Intentify:AI:ApiBaseUrl`
- `Intentify:AI:ApiKey`
- `Intentify:AI:ChatModel`
- `Intentify:AI:EmbeddingModel`
- `Intentify:AI:TimeoutSeconds`
- `Intentify:AI:MaxPromptChars`
- `Intentify:Engage:SessionTimeoutMinutes`

## Key integrations and dependencies
- Cross-module application dependencies: Sites, Knowledge, Leads, Tickets
- Shared package dependency: `Intentify.Shared.AI`, `Intentify.Shared.Web`
- Chat completion client path supports configured HTTP client or null fallback client

## Change-location guide
- Route wiring: `src/Intentify.Modules.Engage.Api/EngageModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Engage.Api/*`
- Chat/bot/application orchestration: `src/Intentify.Modules.Engage.Application/*`
- Domain models: `src/Intentify.Modules.Engage.Domain/*`
- Repositories: `src/Intentify.Modules.Engage.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Engage/tests/Intentify.Modules.Engage.Tests/Intentify.Modules.Engage.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Engage.Api/README.md`
- `src/Intentify.Modules.Engage.Application/README.md`
- `src/Intentify.Modules.Engage.Domain/README.md`
- `src/Intentify.Modules.Engage.Infrastructure/README.md`
- `tests/Intentify.Modules.Engage.Tests/README.md`
