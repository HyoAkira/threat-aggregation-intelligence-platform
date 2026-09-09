## Why

The PRD (FR-004 Deduplication, FR-006 Additive update, FR-009 Whole-batch transaction, US-010/US-011) requires that provider re-fetches commit each batch exactly once: no duplicates, no partial writes, preserved provider attribution, and additive-only data. The domain schema (E02) already enforces the two unique identities, the identity hashing service (E02/E03) exists, and E05 supplies the fetch lifecycle and history shell with its `IProviderFetchExecutor` seam. No shared atomic upsert/dedup pipeline exists yet, so adapters (E06/E07) currently have nothing to write through; this epic delivers that transaction-and-identity pipeline plus query-time false-positive calculation.

## What Changes

- Add **identity resolution** that resolves each incoming provider record by both provider-scoped identities — `(providerId, iocType, iocValueHash, port)` and `(providerId, providerRecordId)` — and confirms a hash lookup by comparing the normalized value to guard against collisions.
- Add **provider-row upsert**: preserve `createdAt` and the stored `firstSeenUtc`, refresh `updatedAt`, replace provider metadata and current tag associations, and replace `lastSeenUtc` with the provider's incoming value. Provider record ID is authoritative and may change that row's normalized IOC identity.
- Add **conflict rejection**: when the new normalized identity for a record already belongs to another provider row, reject the entire fetch rather than merge rows.
- Add **normalized tag replacement**: trim/lowercase/deduplicate tag names, cache tag-name/ID pairs in a scalar dictionary across chunks, insert each new name once, and reference IOC-tag rows by scalar tag ID.
- Add a **whole-batch transaction**: process ~2,000 records per chunk with EF `AddRange`/`SaveChanges` and a cleared change tracker inside one outer transaction, keeping fetch-history outside the transaction. Any invalid record rolls back all IOC/tag changes and records a Failed history with zero counters.
- Make **redelivery idempotent** so repeated or concurrent delivery of the same `jobId` produces the same final dataset with no duplicate rows or false added/updated counts.
- Add **query-time false-positive state**: calculate `updatedAt < now − 180 days` per provider row using the shared UTC clock (TI-085, P1), not persisted.
- **BREAKING**: the worker now performs all DB writes through the atomic pipeline; adapter output (E06/E07) is consumed by the pipeline rather than writing directly. No schema or API contract changes.

## Capabilities

### New Capabilities
- `identity-resolution`: resolving provider records by endpoint identity `(providerId, iocType, iocValueHash, port)` and provider-native identity `(providerId, providerRecordId)`, hash-collision confirmation, and identity-conflict rejection.
- `provider-upsert`: the upsert rules that preserve immutable fields, refresh mutable metadata/timestamps, and replace tag associations for a single provider row.
- `tag-replacement`: normalized tag-name handling and per-chunk scalar-ID tag association replacement.
- `batch-transaction`: chunked whole-batch IOC transaction with bounded EF tracking, atomic rollback, and history written outside the transaction.
- `redelivery-idempotency`: safe handling of unique-key races and message replay so repeated/concurrent delivery cannot duplicate or double-count.
- `false-positive-state`: query-time calculation of the 180-day false-positive flag per provider row using the shared UTC clock.

### Modified Capabilities
- None — `openspec/specs/` behavior is not altered. The shared identity reads and unit-of-work are already specified by `repositories`; E08 adds the pipeline behavior that uses them without changing their requirements.

## Impact

- `src/ThreatIntelligence.Infrastructure` — the atomic pipeline: identity resolver, upsert, tag replacement, and batch transaction; no schema or `.env` changes.
- `src/ThreatIntelligence.Application` — pipeline/executor abstractions and the query-time false-positive calculation consumed by query paths (E09).
- `src/ThreatIntelligence.Worker` — the E05 consumer invokes the pipeline through the existing `IProviderFetchExecutor` seam.
- Dependencies: E02 domain model and unique identities, E02/E03 identity hashing, E05 lifecycle/history/`IProviderFetchExecutor`, E03 provider-record normalization/validation.
- `tests/ThreatIntelligence.UnitTests` — identity resolution, upsert field rules, tag replacement, false-positive boundary, and idempotency logic.
- `tests/ThreatIntelligence.IntegrationTests` — whole-batch commit/rollback, identity conflicts, concurrent/redelivered upserts, and false-positive state against real SQL Server (TI-138).