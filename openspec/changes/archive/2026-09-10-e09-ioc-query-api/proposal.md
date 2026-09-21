## Why

IOCs are ingested and deduplicated (E02/E03/E08) but there is no way to read them back: the API project exposes only a scaffold controller, and the `SearchIocs` query is an unimplemented stub with no reader, controller, or error contract. PRD FR-002/FR-003 and stories US-001/US-002/US-003 require authenticated users to search an exact IOC, inspect a grouped IOC with source attribution, browse and filter the dataset, and trace related hosts/parent domains/URLs.

## What Changes

- Add the authenticated, source-attributed IOC query surface under `/api/iocs`: exact **search** (auto-classified and normalized), **detail** by row id, filtered grouped **browse**, and opt-in **relations** (related hosts, parent domains, and URLs). Response contracts keep every value and tag attributed to the source that supplied it; provider-inapplicable null properties are omitted while valid `false`, `0`, and empty values are retained.
- **Search (`GET /api/iocs/search`)** — `q` is required and treated as **combined search**: the query is auto-classified and normalized (E03 rules), and optional `tag` / `provider` filters narrow a matched group's `sources` instead of returning a different shape. Invalid input returns 400; no matching group (or filters that remove every source) returns 404.
- **Detail (`GET /api/iocs/{id}`)** — resolves a row id to its `(type, normalized value)` group, aggregates `firstSeenUtc = MIN` and `lastSeenUtc = MAX` across all sources, and returns every source entry (same-provider port variants stay separate sources). Missing id returns 404.
- **Relations (`GET /api/iocs/{id}/relations`)** — opt-in and bounded: returned only when the caller requests them, always paginated (`pageSize ≤ 100`), resolving related URL groups by exact `urlHost` or `parentDomain`. No unbounded inline URL arrays. Wire JSON shape is a working contract here and may be restyled before the frontend (E11) consumes it.
- **Browse (`GET /api/iocs`)** — paginated grouped listing filtered by `type`, `provider`, normalized `tag`, and `firstSeen`/`lastSeen` UTC ranges, ordered by an aggregate sortable field (`lastSeenUtc` default) with NULLs last and a stable tie-breaker, plus `totalCount`.
- Add RFC 7807 **ProblemDetails** error handling shared across the API (validation 400, not found 404, auth/forbidden 401/403, conflict 409, unexpected 500), each carrying the request correlation id and safe messages.
- Remove the placeholder `WeatherForecastController`. The existing `SearchIocs` Application stub is reconciled with the combined-search contract (its reader/aggregation seam is kept and completed, not replaced by an unrelated design).
- **BREAKING** for any consumer of the example JSON in Technical Specification §6: the detail `related.urls` inline array is removed in favor of paginated relation queries; tag/provider filtering on search narrows `sources` rather than widening the result.

## Capabilities

### New Capabilities
- `ioc-query-responses`: the shared source-attributed response contract — grouped IOC shape, per-source entries with provider-specific fields and tags, MIN/MAX aggregation, null omission with `false`/`0`/empty retention, the 180-day `isFalsePositive` calculation, and the paginated list envelope with page bounds.
- `ioc-search`: combined exact IOC search with auto-classification/normalization, port vs bare semantics, and optional `tag`/`provider` source filters; 400 invalid-input and 404 not-found behavior.
- `ioc-detail`: grouped IOC detail resolved by row id with aggregate timestamps and complete per-source entries.
- `ioc-relations`: opt-in, bounded, paginated queries for related URLs by exact `urlHost` and registrable `parentDomain`.
- `ioc-browse`: filtered, ordered, paginated grouped browse with `type`/`provider`/`tag`/datetime-range filters, aggregate sort with deterministic NULL/tie ordering, and total counts.
- `api-error-handling`: consistent RFC 7807 ProblemDetails responses across the API with correlation ids and safe error messages.

### Modified Capabilities
- None — no existing `openspec/specs/` capability changes behavior; E09 builds the query surface from the E02 schema, E03 normalization, and the E08 pipeline without altering their contracts.

## Impact

- `src/ThreatIntelligence.Application` — new CQRS queries/handlers and DTOs under `Features/Iocs`; the existing `SearchIocs` stub is adapted to the combined-search contract; normalization services are applied at query time.
- `src/ThreatIntelligence.Infrastructure` — new `IocSearchReader`/browse/relation/detail readers (the `IIocSearchReader` seam currently has no implementation); no schema or configuration changes.
- `src/ThreatIntelligence.API` — `IocsController` (authenticated), ProblemDetails middleware/filters, controller tests; relies on E04 JWT auth policy (`[Authorize]`) and correlation-id logging already in `Program.cs`.
- `tests/ThreatIntelligence.UnitTests` and `tests/ThreatIntelligence.IntegrationTests` — query classification, aggregation, filter/ordering/pagination, and contract tests against SQL Server.
- Dependencies: E02 schema (`iocs`, `tags`, `iocTags`, indexes), E03 normalization/PSL, E08 atomic pipeline output (populated rows), E04 authentication for the HTTP layer. No new `.env` keys.
