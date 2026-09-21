## Context

See proposal.md - Why. The Technical Specification (§7 Messaging consumer flow, §4 schema/identity rules) and decisions TI-004, TI-005, TI-008 pin the atomic-pipeline behavior: one DB transaction per fetch, ~2,000-record chunks with cleared change tracking, both provider identities resolved, provider record ID authoritative, hash lookup confirmed by value, identity conflicts reject the whole fetch, and history written outside the IOC transaction. E02 already ships the schema (unique indexes, `portKey` computed column), the `IoC`/`Tag`/`IoCTag` entities, and `IUnitOfWork`/`IFetchHistoryRepository`; E05 ships the fetch lifecycle, `FetchHistory` state-machine invariants, and the `IProviderFetchExecutor` seam the worker consumer calls. E03 ships normalization/validation producing canonical provider records. No pipeline exists yet to turn a batch of validated provider records into committed IOC rows.

## Goals / Non-Goals

**Goals:**
- A single shared atomic pipeline the worker consumer runs once per fetch, consuming E03 canonical provider records and producing committed IOC/tag rows with accurate added/updated counters.
- Deterministic identity resolution, provider-row upsert, and tag replacement that are idempotent under redelivery and concurrency.
- Bounded memory and EF change-tracking load regardless of feed size, via chunking inside one outer transaction.
- Query-time false-positive calculation reusing the shared UTC `TimeProvider`.

**Non-Goals:**
- No new API endpoints or DTOs (E09); the pipeline exposes the seam E05's consumer and future query code call.
- No schema or migration changes: the E02 unique indexes and `portKey` computed column already exist and are relied on, not recreated.
- No error-classification/retry logic (E05 owns it via MassTransit); the pipeline only reports whether a batch committed or must roll back.
- No scheduling, guards, or cooldowns (E05).
- No provider adapters (E06/E07 already produce the records this consumes); this epic does not change their output contract.
- No tag cache persistence across fetches: the scalar tag dictionary is per-fetch.

## Decisions

- **Pipeline as an Application service implemented in Infrastructure**: define `IIocBatchWriter` (Application) with `ApplyAsync(IReadOnlyList<ProviderRecord>, providerId, CancellationToken)` returning `(added, updated)`. Implement in Infrastructure against EF Core inside the E05-owned transaction. Rationale: one seam for both adapters and testable via fakes; matches the E05 `IProviderFetchExecutor` split. Alternative (pipeline in Application using repository interfaces) rejected because chunked `SaveChanges` and `AddRange` are EF Core concerns best confined to Infrastructure.
- **Transaction ownership**: the pipeline opens and owns the single `IUnitOfWork` transaction for the IOC batch; the E05 lifecycle writes the `FetchHistory` row outside it (per `repositories` spec and TI-004). Rationale: keeps the atomic rollback and the "history survives rollback" rule in one place. Alternative (lifecycle owning the transaction) would couple the two concerns and risk history inside the rollback.
- **Identity resolution order**: resolve by provider-native `(providerId, providerRecordId)` first, since it is authoritative; if absent, resolve by endpoint identity `(providerId, iocType, iocValueHash, port)`. Confirm every hash-based match by comparing the stored `iocValue` to the incoming value (collision guard). If the provider-record row's new normalized identity already belongs to a different provider row, throw an integration-conflict error that aborts the batch. Rationale: the record ID is authoritative (TI-005) and may move a row's normalized identity; endpoint identity handles records that changed ID. Alternative (endpoint-first) could misroute a record whose ID row already exists under a new value.
- **Upsert field policy**: a pure mapper/apply step preserves `createdAt` and stored `firstSeenUtc`, refreshes `updatedAt`, overwrites provider metadata, replaces `lastSeenUtc`, and replaces tag associations. No timestamp-regression guard. Added/updated counts follow TI-004: a resolved existing row increments `updated` (even on identical values); a new row increments `added`. Rationale: exact PRD FR-006 / Tech Spec §7 counter semantics; alternatives that skip "identical update" break `added + updated == records committed`.
- **Tag replacement with a scalar ID cache**: maintain a per-fetch `Dictionary<string,int>` of normalized tag names to IDs, loaded `AsNoTracking` at fetch start. Within each chunk, normalize/dedupe tag names, insert missing names once via `SaveChanges` to obtain IDs, update the dictionary, and reference IOC-tag rows by scalar ID (never tracked `Tag` entities). Rationale: matches Tech Spec §7 and survives change-tracker clearing; the `tags.name` unique index remains the DB safeguard. Alternative (tracking `Tag` entities across chunks) breaks after `ChangeTracker.Clear()`.
- **Chunking**: fixed chunk size ~2,000 provider records; per chunk, `AddRange` pending IOC/tag entities, `SaveChanges`, then `ChangeTracker.Clear()`, all inside the one outer transaction. Rationale: bounded tracking and memory (Tech Spec §7); alternatives (single save of the whole feed) unbounded, (commit-per-chunk) destroys atomicity.
- **False-positive at query time**: a small Application helper taking the shared UTC `TimeProvider` computes `updatedAt < now - 180 days` per provider row; not persisted. Rationale: PRD line 80-81 / Tech Spec §4; alternatives (persisted flag) would need writes and staleness maintenance.
- **Idempotency via unique indexes + transaction**: rely on the E02 unique indexes as the race/duplicate backstop; on a unique-constraint violation from a concurrent insert, the pipeline retries resolution once within the same logical batch decision rather than creating a duplicate or over-counting. Rationale: Tech Spec §7 idempotency summary; alternatives (application-level distributed lock) are deferred by TI-008 and unnecessary under `prefetch=1`.

## Risks / Trade-offs

- [Chunked `SaveChanges` inside a transaction is slower than one bulk insert for very large full feeds] → Accepted for atomicity and bounded memory; TI-136 benchmarks and may tune chunk size later.
- [Hash collision could mis-resolve an identity] → Value comparison after hash lookup confirms equality before treating rows as matching (TI-026 contract).
- [Provider-native ID change racing the endpoint identity] → Provider-record ID is authoritative; conflicts abort the batch (TI-005); integration tests cover the conflict case.
- [Concurrent duplicate delivery could both resolve-to-insert] → Unique indexes reject the loser; the pipeline re-resolves once and does not over-count (TI-084).
- [Tag cache grows with distinct tags per fetch] → Scalar dictionary is per-fetch and bounded by feed tag diversity; acceptable for MVP.
- [False-positive cutoff uses shared clock but not the row's exact 180-day semantics under DST] → UTC `TimeProvider` is used per Tech Spec; boundary is exact 180 days as specified.

## Migration Plan

- Purely additive in Infrastructure/Application: new pipeline service, its interfaces, and the false-positive helper; no schema, API, message-contract, or `.env` changes. Worker `Program.cs` registers `IIocBatchWriter`.
- Rollback: revert the additive pipeline additions; E05's consumer and E06/E07 adapters retain their existing contracts, so no data migration is required.
- The pipeline relies on existing E02 indexes and migrations; no forward or upgrade migration is introduced.

## Open Questions

None — identity, upsert, tag, transaction, idempotency, and false-positive behavior are pinned by the PRD, Technical Specification, and TI-004/005/008 decisions.