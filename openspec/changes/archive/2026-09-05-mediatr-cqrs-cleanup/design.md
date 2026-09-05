## Context

See proposal.md - Why. The Application layer's handers return `int`, controllers re-wrap in anonymous objects, and two Infrastructure files use a `Services` namespace that doesn't match their physical location. The MediatR registration (`AddApplication`) and service abstractions are already in place; this change refines the CQRS contract to fully rely on typed response DTOs.

## Goals / Non-Goals

**Goals:**
- Introduce immutable response DTOs as the sole response type flowing from services → handlers → controllers
- Make the API response shape explicit and type-safe instead of anonymous objects
- Align Infrastructure namespaces with the physical folder layout
- Establish the `tests/` folder with a first test project for the new DTOs

**Non-Goals:**
- No pipeline behaviors (validation/exception) — deferred to the error-handling change
- No FluentValidation validators — commands are parameterless; nothing to validate
- No service-boundary refactor (API fetch + persistence stay combined in `IThreatFoxApiService`/`IPhishTankApiService`)
- No behavior change to the endpoints themselves

## Decisions

### Decision 1: Two shared response DTOs in `Application/DTOs/`

- `DownloadDatasetResponse` — used by both `DownloadThreatFoxDatasetCommand` and `DownloadPhishTankDatasetCommand` (shape: `int IocsProcessed`, `string Message`, `DateTime FetchedAtUtc`)
- `GetRecentIoCsResponse` — used by `GetRecentThreatFoxIoCsCommand` (same shape)

Rationale: both download operations describe "a dataset was fetched and persisted" and share one shape; keeping a distinct type for the recent-IoCs query lets the two evolve independently. Same shape for both keeps mapping trivial.

Alternatives considered: a single generic `OperationResponse` for all three — rejected because it dilutes the query/command distinction the codebase already draws. Anonymous inline objects (current state) — rejected per proposal.

### Decision 2: DTOs are C# records with positional/init-only properties

Records give value-equality, immutability, and concise syntax, satisfying the spec requirement that DTOs have no public setters. They live in namespace `ThreatIntelligence.Application.DTOs`.

### Decision 3: Services construct and return the DTO; handlers pass through

`IThreatFoxApiService.DownloadDatasetAsync` / `RecentIoCsAsync` and `IPhishTankApiService.DownloadDatasetAsync` change their return type from `Task<int>` to `Task<DownloadDatasetResponse>` / `Task<GetRecentIoCsResponse>`. The Infrastructure implementations build the DTO (count from the upsert loop, message describing the operation, `FetchedAtUtc = DateTime.UtcNow`), and handlers forward it unchanged. Controllers return the DTO directly.

Rationale: the DTO is the cross-layer contract (per the modified mediatr-pattern spec); constructing it where the count is produced avoids double-wrapping or identity loss. The `Message` field is set by Infrastructure to describe the completed operation (`"Download completed"`, `"Recent IOCs fetched"`).

### Decision 4: Rename the response field to `IocsProcessed` / `PhishEntriesProcessed`

The current controllers distinguish `IocsProcessed` (ThreatFox) vs `PhishEntriesProcessed` (PhishTank). Since both downloads now share `DownloadDatasetResponse`, a single field name cannot satisfy both. Using `RecordsProcessed` as the shared count field keeps the type generic while the HTTP docs and message provide context. This is a deliberate API response-field rename: `IocsProcessed`/`PhishEntriesProcessed` → `RecordsProcessed`.

### Decision 5: Namespace fixes in Infrastructure root files

`ThreatIntelligence.Infrastructure/DependencyInjection.cs` and `ThreatIntelligence.Infrastructure/ThreatIntelDbContext.cs` move from `namespace ThreatIntelligence.Infrastructure.Services` to `namespace ThreatIntelligence.Infrastructure`. This matches their physical location and the `Services/` subfolder files that keep `ThreatIntelligence.Infrastructure.Services`. Namespace-only change; no behavior.

### Decision 6: Test project `tests/ThreatIntelligence.Application.Tests`

xunit test project targeting net10.0, referencing the Application project. Contains unit tests for the new DTOs (construction, immutability, and value-equality). Handler/service behavior tests are deferred to the error-handling change where mocking infrastructure is introduced.

## Risks / Trade-offs

- [API response field rename (`IocsProcessed` → `RecordsProcessed`)] → Consumers depending on the JSON field name will break; acceptable pre-MVP, no known consumers yet.
- [Test project is new tooling for the repo] → Follows the declared `.slnx` `/tests/` folder; standard xunit setup reduces novelty.
- [Shared `DownloadDatasetResponse` hides which provider was fetched] → The response `Message` and the endpoint URL preserve that context; splitting later is a non-breaking additive change.

## Migration Plan

No data migration. Deploy is a code change: update services, handlers, controllers, add DTOs, build, run tests.

## Open Questions

None.