# Anti-Circular Dependency Rules (Mandatory)

## What Causes Circles (Do Not Do)
- Module A references Module B Infrastructure
- Shared package references a module
- Contracts depend on Infrastructure
- Domain depends on Application or Api

## Allowed Graph (Example)
Shared.* → used by Modules.*
Module.Contracts → used by Module.Application + Module.Api
Module.Domain → used by Module.Application + Module.Infrastructure
Module.Infrastructure → used only by Module.Api/Host composition (never by other modules)

## Checklist Before You Add a Reference
- Is there already a shared helper/DTO/validator that solves this?
- Can this dependency be moved to Contracts or Shared.Abstractions?
- Is this a cross-module need? If yes, use Contracts or Messaging.

If unsure, STOP and ask.
