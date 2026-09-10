## Context

See proposal.md - Why. The Technical Specification (§7 Messaging, §9 Worker Scheduler) and decisions TI-002, TI-003, TI-008 specify MassTransit over RabbitMQ with durable topology, publisher confirmation, no outbox, no custom retry topology/DLX, one-at-a-time consumption, and logical-history reuse. The E02 changes already ship `FetchHistory` (with state-machine invariants: Queued/Running have no finish/error; Failed requires finish + non-blank error + zero counters), `Provider`, `IFetchHistoryRepository`, `IUnitOfWork`, `TimeProvider`, and `RabbitMqOptions` (Host/Port/User/Password/Exchange) bound from `.env`. No MassTransit packages exist yet; the worker currently runs an idle `BackgroundService`, and the API has no messaging wiring.

## Goals / Non-Goals

**Goals:**
- One shared, validated `FetchJob` contract compiled into both API and worker.
- Durable MassTransit topology with publisher confirmation, prefetch 1, consumer concurrency 1, and the default `_error` queue.
- Atomic guard reservation (capabilities, cooldowns, enabled/running) checked before publish and rechecked at consume. Scheduling uses each provider's configured interval; PhishTank is seeded with 8 hours.
- Manual trigger endpoints and a scheduler loop both publishing through the same guard + publish path.
- One logical history row per `jobId` reused across retries/redelivery, with exact counter semantics.
- Error classification: transient retried 10s/30s/1min then `_error`; 429, invalid-record, auth, and conflict never retried.

**Non-Goals:**
- No transactional outbox (accepted DB-to-broker crash window, TI-008).
- No custom retry topology, scheduler, or DLX; MassTransit's default `_error` only (TI-002).
- No configurable fetch concurrency or distributed provider locks (TI-058, P2, deferred).
- No provider HTTP client retries; MassTransit owns retries (TI-003).
- No adapter/ingestion pipeline implementation here (E06/E07/E08); this epic delivers the lifecycle shell, history transitions, and guards, with adapter invocation as an injected seam.

## Decisions

- **`FetchJob` contract location**: Define `FetchJob` as a POCO record in `ThreatIntelligence.Application` (contracts), referenced by both API and Worker. Validate on deserialize via MassTransit's message serialization config (JSON, explicit type). Rationale: a single compiled contract guarantees publisher/consumer agreement without a separate shared-package split; alternatives (contracts in Infrastructure, or duplicated types) create circular dependency or drift risk.
- **MassTransit wiring**: Register MassTransit in both API and Worker with `AddMassTransit` + `UsingRabbitMq`, configured from existing `RabbitMqOptions` (`tieto.fetches` exchange, routing key `fetch`). API only publishes (no consumers); Worker consumes `FetchJob`. Durable exchange/queue + persistent messages via default MassTransit settings; publisher confirmation via `UsePublishFilter`/`ConfigurePublish` confirmation semantics (`WaitForConfirmation` or `UsePublishMessageRetry` is NOT used — publish confirmation is awaited natively). Rationale: matches Tech Spec §7; alternatives (raw RabbitMQ client, separate topology declarations) add hand-rolled durability and confirmation code.
- **One-at-a-time consumption**: Configure `PrefetchCount = 1` and consumer concurrency `1` (MassTransit `ConsumerLimit`/`UseMessageRetry` does not affect concurrency). Rationale: MVP processes one fetch globally; a distributed lock is intentionally avoided (TI-008).
- **Guard service as a single seam**: Implement `IFetchGuard` (Application) with `ReserveAsync(providerId, trigger, mode)` returning an `Allow | Reject(reason)` decision, implemented in Infrastructure against `fetchHistory`/`providers` using a transaction that atomically inserts the Queued history row and returns its `jobId`. Both manual endpoints and the scheduler call this same seam. Cooldown reads the latest matching `fetchHistory.startedAt` by provider/mode. Consumer rechecks the same persisted state (cheap read) before executing. Rationale: one code path guarantees consistent guard math and no double reservation; alternatives (inline checks in each caller) risk divergence.
- **History lifecycle service**: A `FetchLifecycle` service (Application) owns transitions: publisher inserts Queued → worker marks Running at first execution → terminal Success/Failed. It resolves the row by `jobId` and updates it plus `providers.lastFetchStatus`/`lastFetchAt`/`nextFetchTime` (first-success initialization + interval advancement). Provider's `nextFetchTime` advancement lives in the scheduler publish path and first-success path, consistent with Tech Spec §9. Rationale: keeps EF state-machine invariants in one place; mirrors E02's repository contract.
- **Error classification**: A `FetchErrorClassifier` maps exceptions into `Transient` (network/timeout/5xx/temp-DB) vs `Terminal` (429/quota, validation/invalid-record, auth, integration-conflict). MassTransit `UseMessageRetry` with `Interval(3, [10s, 30s, 1m])` handles transient retries; terminal errors throw a non-retryable exception (`RejectFuturePublish` style is avoided — we use `Fault` policy by throwing `TerminalFetchException`, which MassTransit does not retry). Rationale: matches FR-008/TI-003 attempt semantics exactly; alternatives (manual retry loops in consumer) fight MassTransit's ack model.
- **Adapter seam**: The consumer calls an `IProviderFetchExecutor` abstraction (implemented by E06/E07 adapters later) returning per-batch results; this epic provides the shell and a fake for tests. Rationale: keeps E05 testable without adapter code.

## Risks / Trade-offs

- [Publisher confirmation semantics across MassTransit versions vary] → Mitigation: pin the package version and write an integration test asserting a published message is confirmed and the history row remains Queued.
- [DB-to-broker crash window loses a reserved Queued row without a published job] → Accepted per TI-008; documented; the row stays Queued and is surfaced as stuck-Running/Queued in E10 operator tooling.
- [Consumer recheck still races the atomic reservation] → Mitigation: reservation is transactional and the consume-time check is a guard-of-last-resort that skips rather than double-executes; integration tests cover duplicate delivery.
- [429 handling depends on the adapter signaling quota explicitly] → Mitigation: the executor abstraction returns a typed result (or throws `QuotaExceededException`) so classification is deterministic regardless of provider.
- [Health check currently probes TCP only] → No change needed for this epic; E10/E14 own deeper broker health.

## Migration Plan

- Add `MassTransit` + `MassTransit.RabbitMQ` packages to API and Worker; wire DI in both.
- Worker `Program.cs` gains MassTransit host + the scheduler `BackgroundService` (replacing the idle loop) and the `FetchJob` consumer.
- API `Program.cs` registers publishing; no new config keys (`RABBITMQ__*` already exist in `.env.example`).
- Deploy/rollback: messaging is additive; rolling back removes the bus registration and trigger endpoints. History rows created by the new endpoints are backward-compatible with E02's `fetchHistory` schema.
- `nextFetchTime` behavior change (seeded providers start NULL and wait for first successful manual fetch) is already the E02 seed contract; this epic implements the advancement rules.

## Open Questions

None — contract, guards, topology, and retry semantics are pinned by the PRD, Technical Specification, and TI-002/003/008 decisions.
