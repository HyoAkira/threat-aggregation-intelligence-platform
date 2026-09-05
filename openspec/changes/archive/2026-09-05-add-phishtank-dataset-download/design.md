## Context

The project is a .NET 10.0 Clean Architecture solution (Domain/Application/Infrastructure/API).
The `providers.PhishTank` table, `PhishTank` entity, `PhishTankEntityConfiguration`,
and the EF migration (unique index on `PhishId`) all exist. The `ThreatFoxApiService`
already implements the fetch→parse→upsert pattern for a different data source.
The `DependencyInjection.cs` in Infrastructure currently registers `ThreatIntelDbContext`,
`DatabaseHealthService`, and `ThreatFoxApiService` with HttpClient. No `PhishTankApiService`
exists. The PhishTank data endpoint at `http://data.phishtank.com/data/` serves
`online-valid.json.gz` without authentication.

## Goals / Non-Goals

**Goals:**
- Fetch the PhishTank online-valid dataset, decompress gzip,
  parse the JSON array, and upsert into `providers.PhishTank` on `PhishId`.
- Follow the same architectural pattern as `ThreatFoxApiService` (HttpClient injection,
  raw SQL MERGE for upsert, controller-based manual triggering).
- Use only .NET built-in libraries (no new NuGet packages).

**Non-Goals:**
- Background/timer-based scheduling (deferred to Epic 2.3 in project.md).
- Implementing the `IThreatSourceProvider` interface pattern (not yet created).
- Using an API key for higher rate limits — the public endpoint suffices.
- Normalized `Ioc` table (Epic 3.1) — storing raw PhishTank rows only in this change.

## Decisions

### 1. HTTP client: HttpClient (not Refit)
**Chosen**: Use `HttpClient` via `IHttpClientFactory`, same as ThreatFox.
**Alternatives considered**: Refit — mentioned in project.md Epic 2.2 for future phases,
but no Refit package is installed. `HttpClient` is sufficient and avoids new dependencies.
**Rationale**: Consistent with the existing ThreatFox implementation; minimal dependencies.

### 2. Decompression: GzipStream (not ZipArchive)
**Chosen**: Use `System.IO.Compression.GzipStream` to decompress `.json.gz`.
**Alternatives considered**: `ZipArchive` (used by ThreatFox) — PhishTank uses gzip, not ZIP.
**Rationale**: PhishTank serves `.json.gz`, not `.zip`. `GzipStream` is built-in to .NET.

### 3. JSON parsing: JsonDocument with flat array enumeration
**Chosen**: Use `JsonDocument.Parse()` and enumerate the root as a JSON array.
**Alternatives considered**: Strongly-typed `JsonSerializer.Deserialize<List<Dto>>` —
but the `details` nested array varies and needs to be stored as a serialized JSON string,
making schema-less parsing more flexible and consistent with the ThreatFox approach.
**Rationale**: Matches the ThreatFox pattern of using `JsonDocument` with `TryGetProperty`
for field-safe parsing. The flat array shape (vs ThreatFox's keyed object) means
`EnumerateArray()` on the root element instead of `EnumerateObject()`.

### 4. Boolean conversion: "yes"/"no" string → bit
**Chosen**: Convert PhishTank's `"yes"`/`"no"` string values for `verified` and
`online` fields to `bool`/`bit`.
**Rationale**: The PhishTank API returns booleans as strings (see ApiDocs), unlike
ThreatFox which returns native JSON booleans. The spec requires this conversion.

### 5. Upsert: SQL Server MERGE on PhishId
**Chosen**: Use raw SQL `MERGE INTO` statement upsert on `PhishId`.
**Alternatives considered**: EF Core `ExecuteUpdate`/`ExecuteInsert` — requires loading
all records into memory and comparing. Check-then-insert/update — prone to race conditions.
**Rationale**: `MERGE` is a single atomic SQL statement, handles both insert and update
in one pass. SQL Server natively supports it. Consistent with ThreatFox approach but
simpler (single-column key vs three-column composite key).

### 6. Service location: Infrastructure layer
**Chosen**: Place `PhishTankApiService` in `ThreatIntelligence.Infrastructure/Services/`.
**Rationale**: Same as ThreatFox — depends on `ThreatIntelDbContext`, `HttpClient`,
and follows Dependency Inversion principle (API layer injects it).

### 7. Manual trigger: API controller endpoint
**Chosen**: Create `PhishTankController.cs` in `ThreatIntelligence.API/Controllers/`
with `GET /api/phishtank/download`.
**Rationale**: Current scope is manual invocation only (no background service). A
controller endpoint provides the simplest trigger mechanism, consistent with ThreatFox.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| **Dataset is large** (potentially millions of rows) | Process record-by-record with individual MERGE calls; if memory pressure occurs, use streaming/`yield` pattern |
| **PhishTank rate limits without API key** | Public endpoint allows limited downloads per day; document API key usage as a future enhancement |
| **Gzip decompression failure** | Wrap in try-catch with descriptive exception message |
| **JSON field mapping drift** | Use `TryGetProperty` with fallbacks for each field; handle null/missing gracefully |
| **SQL MERGE deadlock potential** | Unlikely at our throughput; if issues arise, switch to batch `ExecuteUpdate` |

## Migration Plan

Greenfield implementation — no migration needed:
1. The `providers.PhishTank` table already exists with all columns and unique constraint
2. No database schema changes required
3. No configuration changes required (no API key)
4. Register the new service in `DependencyInjection.cs`
5. Deploy the new controller alongside existing controllers

## Open Questions

None — all decisions were resolved by following the established ThreatFox pattern
and adapting to PhishTank's documented data format.
