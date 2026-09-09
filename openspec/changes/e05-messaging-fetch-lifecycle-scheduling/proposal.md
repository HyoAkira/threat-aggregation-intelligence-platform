## Why

The PRD (FR-007, FR-008, §5 constraints) and Technical Specification (§7, §9) require guarded, durable, observable fetches moving from the API and scheduler to a single bounded worker over RabbitMQ. Nothing exists today: there is no `FetchJob` contract, no MassTransit wiring, no trigger guards, no scheduler, and no fetch-history lifecycle, so providers cannot be collected at all.

## What Changes

- Add a versioned `FetchJob` message contract (`jobId`, `providerId`, `trigger`, `mode`, `attempt`) shared by API and worker with strict serialization validation.
- Provision durable RabbitMQ topology via MassTransit: durable exchange/queue, persistent messages, publisher confirmation, prefetch 1, consumer concurrency 1, and the default `_error` queue for exhausted messages. No custom retry topology, scheduler, or DLX for the MVP.
- Add a trigger guard service that atomically reserves an allowed logical fetch, enforcing provider enabled state, queued/running state, and ThreatFox cooldowns (recent ≥ 5 min, full ≥ 60 min) before publish, and re-checks at consume time. PhishTank follows its configured provider interval; its seeded default is 8 hours.
- Add manual fetch publisher endpoints: `POST /api/admin/providers/{id}/fetch` (default mode) and `POST /api/admin/providers/{id}/fetch/full` (ThreatFox only); 202 after confirmed publication, 400 for unsupported modes, 409 for guarded triggers.
- Add a worker scheduler loop that polls enabled/due providers, applies guards, publishes scheduled jobs, and advances `nextFetchTime` using each provider's configured interval.
- Enforce one-at-a-time fetch execution and implement the fetch-history lifecycle: one uniquely keyed logical history row reused across reservation, retries, and redelivery, transitioning Queued/Running → Success/Failed with counter and error details.
- Add error classification and retries: transient errors retried by MassTransit after 10 s / 30 s / 1 min then to `_error`; no retry for 429/quota, invalid-record, authentication, or integration-conflict errors.

## Capabilities

### New Capabilities
- `fetch-job-contract`: the versioned `FetchJob` message contract and its serialization/validation rules shared by API and worker.
- `messaging`: durable MassTransit/RabbitMQ topology, publisher confirmation, bounded consumption, and default `_error` routing.
- `trigger-guards`: atomic reservation and guard evaluation (enabled, running, cooldowns) before publish and at consume time.
- `manual-fetch-triggers`: admin endpoints that validate provider capabilities, apply guards, persist a Queued history row, and publish a `FetchJob`.
- `fetch-scheduler`: the worker loop that publishes due scheduled fetches and manages `nextFetchTime` from provider intervals.
- `fetch-execution`: one-at-a-time consumer execution, history lifecycle transitions, counter semantics, and error classification/retry behavior.

### Modified Capabilities
- None — `openspec/specs/` is empty; all capabilities are new.

## Impact

- `src/ThreatIntelligence.Application` — `FetchJob` contract, guard evaluation, error classification, and fetch-history lifecycle abstractions; reuses the existing `IFetchHistoryRepository`, `IUnitOfWork`, and `TimeProvider`.
- `src/ThreatIntelligence.Infrastructure` — MassTransit bus registration, guard implementation, scheduler, consumer, and history lifecycle service; consumes the existing `RabbitMqOptions` (Host/Port/User/Password/Exchange).
- `src/ThreatIntelligence.API` — manual fetch endpoints under `/api/admin/providers` (E10 admin surface, but the trigger endpoints are required here because they publish `FetchJob`).
- `src/ThreatIntelligence.Worker` — scheduler `BackgroundService` and the MassTransit consumer host replacing the current idle worker loop.
- New NuGet packages: `MassTransit`, `MassTransit.RabbitMQ` (API and Worker); no new configuration keys (RabbitMQ settings already exist in `.env.example`).
- `tests/ThreatIntelligence.UnitTests` — guard math, error classification, contract validation.
- `tests/ThreatIntelligence.IntegrationTests` — publish confirmation, retry delay/cap, redelivery, `_error` routing, history reuse, one-at-a-time consumption against a real RabbitMQ instance.
