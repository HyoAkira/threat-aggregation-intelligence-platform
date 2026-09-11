## Context

See proposal.md - Why. The query surface does not exist yet: `ThreatIntelligence.API` has only a scaffold `WeatherForecastController`; auth middleware (E04) is not wired; there is a thin, unfinished Application stub `SearchIocsHandler` → `IIocSearchReader` with no Infrastructure implementation and no DI registration. Reusable pieces already present: E03 normalization services and `NormalizationError` taxonomy in Application, `IValueHasher` (SHA-256), `IocRepository` helpers (`GetGroupedAsync` verifies stored value after hash lookup), `TimeProvider` singleton, `IoCTag`/`Tag` join tables, and Serilog correlation-id request logging. E02 defined the schema (indexes on `(iocType, iocValueHash)`, `(iocType, urlHost)`, `(iocType, parentDomain)`, `(lastSeenUtc)`); E08 populates rows. E09 adds read paths only — no schema or `.env` changes.

## Goals / Non-Goals

**Goals:**
- One authenticated, source-attributed IOC query surface (search/detail/relations/browse) that returns complete groups without flattening provider attribution.
- Query-time classification and normalization shared with ingestion (E03), never duplicated in SQL.
- Deterministic, index-backed ordering and pagination on every multi-item endpoint; bounded relation payloads.

**Non-Goals:**
- No writes, no admin authorization logic, no fetch trigger UI/API (E05/E10), no fuzzy/full-text search (PRD out of scope).
- No new persistence columns or indexes; no caching layer in this change.
- Not defining the final presentation JSON consumed by the frontend beyond the behavior contract in the specs (E11 restyling allowed).

## Decisions

### D1 — Normalize and classify in Application; read in Infrastructure
Search inputs are classified/normalized by the existing E03 pipeline inside the MediatR handler, producing a canonical `(IocType, iocValue, port?)`. The Infrastructure reader then takes canonical keys, never a raw string, and never re-implements classification.
*Rationale:* one shared normalization path for ingestion and query (PRD FR-005, spec §5.5); pure and unit-testable; keeps SQL free of parser logic.
*Alternative rejected:* classify inside the reader in SQL — duplicates E03 logic and defeats the shared-service contract.

### D2 — Replace the `IIocSearchReader` stub seam with a query read-model surface
The unfinished `SearchIocsHandler`/`IIocSearchReader` pair (raw `query`/`tag` strings, no implementation) is reshaped into an `IIocQueryReader` in Application with small methods that return fully assembled group records: `SearchGroupAsync`, `GetDetailGroupAsync`, `BrowseAsync`, `GetRelatedAsync`. Group assembly (mapping rows + provider names + tags into the source-attributed records) happens in Infrastructure.
*Rationale:* the group record shape is shared across all four endpoints; centralizing assembly prevents each endpoint from re-deriving provider-specific mapping.
*Alternative rejected:* four ad-hoc query classes each building groups — duplicates the fragile attribution mapping.

### D3 — Hydrate groups with two-stage, batched reads (no N+1)
- **Stage 1 (page/key query):** for browse, a single grouped SQL projection over `iocs` produces the page's group keys `(iocType, iocValue)` with aggregate `MIN(firstSeenUtc)`/`MAX(lastSeenUtc)`, applying tag/provider/type/datetime predicates and `ORDER BY ... OFFSET ... FETCH`, plus a separate `COUNT` of distinct groups for `totalCount`. Tag filtering is a join to `iocTags`/`tags`; provider filtering on `providerId`.
- **Stage 2 (hydration):** fetch the rows for those group keys plus provider names in one `IN` query, then fetch tag names for all page source row ids in one `iocTags`+`tags` join; assemble groups in memory.
Search and detail use the same stage-2 hydration after a stage-1 that resolves keys by `(iocType, iocValueHash)` (collision-checked against stored `iocValue`) — reusing `IocRepository`'s existing hash/verify helpers.
*Rationale:* bounded round-trips regardless of group size; tag data rides the existing `(iocType, urlHost)` / `(lastSeenUtc)` indexes; avoids loading whole tables to page.
*Alternative rejected:* one giant `Include` graph — loads tag rows for every match and materializes unbounded state.

### D4 — Aggregate ordering with explicit NULLS-last emulation and stable tie-break
Sort keys are the group aggregates `MAX(lastSeenUtc)` (default) and `MIN(firstSeenUtc)`, plus `iocType`/`iocValue`. SQL Server has no `NULLS LAST`, so the reader orders by `CASE WHEN <key> IS NULL THEN 1 ELSE 0 END` then `<key> ASC|DESC`, then `iocType ASC`, then `iocValue ASC`. `orderBy`/`dir` are validated enums (400 on anything else).
*Rationale:* deterministic pages per the agreed contract; NULLs always last regardless of direction.
*Alternative rejected:* relying on default SQL null placement — non-deterministic across directions and databases.

### D5 — Null omission at serialization, not in the DTO
Response DTOs carry nullable provider-specific fields and are serialized with JSON ignore-on-null. Stored `false`/`0`/empty values are non-null and therefore retained automatically. `isFalsePositive` is computed per source during mapping from `updatedAt` against the injected `TimeProvider` (`updatedAt < now - 180 days`), never persisted.
*Rationale:* satisfies FR-003 (null provider-inapplicable omitted, valid false/zero/empty retained) with one global policy rather than per-field annotations.

### D6 — One authenticated `IocsController`
`GET /api/iocs/search`, `GET /api/iocs/{id}`, `GET /api/iocs/{id}/relations`, and `GET /api/iocs` live on a single controller marked `[Authorize]` (any authenticated user; admin-only concerns stay in E10's `/api/admin/*`). Route-model + query-param validation produce ProblemDetails via a shared exception/validation mapping. The scaffold `WeatherForecastController` is deleted.
*Rationale:* matches the §6 API table; the existing correlation-id logging middleware already stamps responses, and its value is folded into ProblemDetails by reading the same header/trace id.

### D7 — Relations are opt-in paginated reads over stored host columns
Relations resolve the seed group's `urlHost`/`parentDomain` from its rows, then run a stage-1 page query on stored `urlHost` (URL rows) or `parentDomain` (URL/domain rows) using the relation indexes, excluding the seed group identity, followed by stage-2 hydration. The detail endpoint never inlines relation lists.
*Rationale:* bounded payloads always (spec §6 example's inline `urls` array is dropped per decision in the proposal); index-backed exact matches.
*Alternative rejected:* inlining related URLs in detail — unbounded for popular hosts.

## Risks / Trade-offs

- [Grouped browse performance on large tables] → Group keys, filters, ordering, and `COUNT` all stay in single SQL projections backed by existing indexes; the tagged/paged path is exercised in integration tests with realistic volumes before E13 benchmarks; the two-stage design keeps per-page work O(page + group rows).
- [EF translation of grouped `ORDER BY`/`HAVING` and `NULLS-last` CASE] → Keep the grouped query in a form EF translates cleanly (no client evaluation); assert generated SQL shape in integration tests; fall back to a small raw-SQL key query if EF regresses, keeping stage-2 hydration in EF.
- [Hash-collision safety] → Search still verifies stored `iocValue` after the hash lookup (existing `ValueEquality` path); browse groups by stored `iocValue` directly, so collision ambiguity cannot surface there.
- [Detail/search group recompute after combined filters] → Aggregates recompute over the narrowed `sources`, matching the response contract; documented in spec scenarios.
- [E04 auth not yet present] → Handlers/readers are tested at unit level with fakes now; controller authorization integration tests are marked dependent on E04's JWT policy and run once it lands.
- [Serialization omit-null global policy could hide intended nulls] → DTO shape is asserted by golden JSON contract tests covering every typed field, so a regressed field fails CI.

## Migration Plan

No database migration, configuration, or `.env` change. Deploy by shipping the new read surface and deleting the scaffold controller; the changed `SearchIocs` internals and the removed inline `related.urls` JSON are the only behavioral deltas and are covered by the API contract tests in this change. Rollback is a source revert of Application/Infrastructure/API — no data backfill required.

## Open Questions

None that change what gets built. The exact wire-JSON field naming of the relation payloads and combined-search detail is a working contract in the specs and may be restyled when the frontend (E11) consumes it; backend behavior and bounds are fixed here.
