## Why

The Application layer currently returns raw `int` counts from handlers, and controllers embed them in anonymous objects. This couples API response shape to implementation details, makes the CQRS pattern incomplete, and prevents type-safe responses.

## What Changes

- Add response DTOs (`DownloadDatasetResponse`, `GetRecentIoCsResponse`) in `Application/DTOs/`
- Change MediatR commands/handlers to return DTOs instead of `int`
- Update service abstractions (`IThreatFoxApiService`, `IPhishTankApiService`) to return DTOs
- Update controllers to return DTOs directly instead of anonymous objects
- Fix namespace inconsistencies in Infrastructure root files

## Capabilities

### New Capabilities

- `application/dto-responses`: Defines the typed response DTOs returned by MediatR commands and their shape.

### Modified Capabilities

- `architecture/mediatr-pattern`: response types change from integer counts to typed DTOs

## Impact

- `ThreatIntelligence.Application/DTOs/` — new DTO files
- `ThreatIntelligence.Application/Features/**` — command & handler return types
- `ThreatIntelligence.Application/Abstractions/*.cs` — service interface signatures
- `ThreatIntelligence.API/Controllers/*.cs` — response shaping
- `ThreatIntelligence.Infrastructure/DependencyInjection.cs`, `ThreatIntelDbContext.cs` — namespace fixes
- `tests/` — new test project