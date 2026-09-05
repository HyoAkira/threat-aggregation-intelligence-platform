## Context

The entities have been moved to `ThreatIntelligence.Domain.Entities` but migration files
still reference the old `ThreatIntelligence.Infrastructure.*` namespace for type resolution.
Additionally, the project was supposed to have MediatR + CQRS from the start (Epic 1.2 in
project.md) but controllers directly inject Infrastructure services instead.

## Goals / Non-Goals

**Goals:**
- Fix migration type-name references so EF Core runtime works correctly
- Implement proper Clean Architecture: Application layer owns abstractions + use cases
- Controllers depend only on IMediator

**Non-Goals:**
- Moving service implementations to a different layer (they correctly stay in Infrastructure)
- Adding new API endpoints or changing response formats
- Changing the database schema or generating a new migration

## Decisions

### 1. Service interfaces go in `Application.Abstractions/`
**Choice:** `IThreatFoxApiService` and `IPhishTankApiService` defined in
`ThreatIntelligence.Application.Abstractions/` with the same method signatures
as the concrete classes (`DownloadDatasetAsync`, `RecentIoCsAsync`).
- **Rationale:** Application layer owns the contract (Clean Architecture). Infrastructure
  implementations depend on these interfaces at registration time only.

### 2. Commands return integer count (matching current controller behavior)
**Choice:** `DownloadThreatFoxDatasetCommand` and `DownloadPhishTankDatasetCommand`
return `int` (the count of processed entries), matching existing controller responses.
- **Rationale:** Preserves API contract. `GetRecentThreatFoxIoCsCommand` also returns `int`.

### 3. Migration files updated in-place (no new migration generated)
**Choice:** Directly edit the 3 Designer files + ModelSnapshot to replace
`"ThreatIntelligence.Infrastructure.PhishTank"` → `"ThreatIntelligence.Domain.Entities.PhishTank"`
etc.
- **Rationale:** The table schema hasn't changed — only the .NET type location. Generating
  a new migration would create unnecessary noise. The existing migrations already created
  the tables with the correct schema; this is purely fixing EF's type-name strings.

### 4. EntityTypeConfiguration classes remain in Infrastructure
**Choice:** EF Core `IEntityTypeConfiguration<T>` classes stay at
`ThreatIntelligence.Infrastructure.Services.EntityTypeConfigurations`.
- **Rationale:** They depend on `Microsoft.EntityFrameworkCore` — they are infrastructure
  code, not domain code.

### 5. FluentValidation registered even if no validators exist yet
**Choice:** `AddValidatorsFromAssembly` registers FluentValidation in `AddApplication`,
even though no validators are implemented in this change.
- **Rationale:** The project.md specifies MediatR + CQRS skeleton. This provides the
  infrastructure for future validators without adding unnecessary validators now.

## Risks / Trade-offs

- **Migration file editing is error-prone** → Mitigation: only change the fully-qualified
  type name strings, not the schema properties. Verify with `dotnet build` and `openspec validate`.
- **Breaking change to controller constructors** → Mitigation: this is an internal API;
  no external consumers exist. The OpenAPI/Scalar reference will regenerate automatically.