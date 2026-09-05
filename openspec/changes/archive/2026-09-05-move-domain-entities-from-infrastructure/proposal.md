## Why

The entity move from Infrastructure to Domain is complete but has left two issues:

1. **Migration type names are stale** — EF Core migration files still reference
   `"ThreatIntelligence.Infrastructure.PhishTank"` and `"ThreatIntelligence.Infrastructure.ThreatFox"`,
   but these entities are now at `ThreatIntelligence.Domain.Entities`. The
   `ModelSnapshot` and Designer files must be updated or EF Core will fail at
   runtime.

2. **Missing MediatR architecture** — The `project.md` calls for "MediatR + CQRS
   skeleton" in Epic 1, but it was never implemented. Currently, API controllers
   depend directly on `Infrastructure.Services.ThreatFoxApiService` and
   `PhishTankApiService` (concrete classes), violating Clean Architecture's
   dependency rule: the Application layer should own the service abstractions
   and use-case handlers.

## What Changes

- Fix migration designer/snapshot files: update fully-qualified entity type names
  from `ThreatIntelligence.Infrastructure.*` → `ThreatIntelligence.Domain.Entities.*`
- Add service interfaces in `Application.Abstractions/`:
  - `IThreatFoxApiService`
  - `IPhishTankApiService`
- Implement MediatR handlers in `Application.Features/`:
  - `DownloadThreatFoxDatasetCommand` + handler
  - `GetRecentThreatFoxIoCsCommand` + handler
  - `DownloadPhishTankDatasetCommand` + handler
- Refactor controllers to dispatch MediatR requests instead of injecting services
- Remove unused `IConfiguration` dependency from controllers
- Register MediatR + FluentValidation in `AddApplication` / `Program.cs`

## Capabilities

### New Capabilities

- `architecture/mediatr-pattern`: MediatR CQRS handlers in Application layer for
  ThreatFox and PhishTank data operations; service interfaces exposed via
  `Application.Abstractions`

### Modified Capabilities

- `persistence/provider-schema`: Adds new requirement specifying that entity
  types in migration metadata must reference `Domain.Entities.*` namespace (type-name-only
  change, no schema change)

## Impact

- Controllers: constructor dependencies change (MediatR instead of services)
- New files: 2 interfaces, 4 MediatR handlers, 2 DTOs
- Migration designer files: type names updated (no new migration)
- `dotnet build` should remain green; runtime behavior unchanged
- BREAKING: `IConfiguration` no longer injected in controllers (was unused)