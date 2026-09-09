## 1. FetchJob contract

- [ ] 1.1 Add `FetchJob` record to Application (jobId Guid, providerId int, trigger, mode, attempt) with strict constructor validation (non-empty jobId, positive providerId, defined trigger/mode, attempt ≥ 1) and verify unit tests cover invalid values
- [ ] 1.2 Configure MassTransit JSON serialization for `FetchJob` in both API and Worker and verify a contract test round-trips a golden message with all fields matching
- [ ] 1.3 Add API/worker shared contract test (same assembly reference, byte-for-byte serialization) and verify it passes in the build

## 2. MassTransit topology and bus wiring

- [ ] 2.1 Add `MassTransit` and `MassTransit.RabbitMQ` packages to API and Worker and verify restore/build succeeds
- [ ] 2.2 Configure MassTransit in both projects from `RabbitMqOptions` (Host/Port/User/Password/Exchange `tieto.fetches`, routing key `fetch`) and verify startup connects to RabbitMQ with no exceptions
- [ ] 2.3 Configure durable exchange/queue with persistent messages, publisher confirmation on publish, `PrefetchCount = 1` and consumer concurrency 1, and verify broker restart keeps topology and an unacked message on the queue
- [ ] 2.4 Add an integration test proving a published `FetchJob` is confirmed, redelivery reuses the same logical job, and an exhausted message lands in the default `_error` queue

## 3. Trigger guard service

- [ ] 3.1 Add `IFetchGuard.ReserveAsync(providerId, trigger, mode)` abstraction in Application and verify it compiles
- [ ] 3.2 Implement guard in Infrastructure: capability check (ThreatFox recent/full, PhishTank full), enabled check, queued/running check, and ThreatFox cooldown (recent ≥ 5 min, full ≥ 60 min from latest `fetchHistory.startedAt`), all evaluated in one atomic transaction that inserts the Queued history row and returns its `jobId`
- [ ] 3.3 Add unit tests for cooldown boundaries, disabled provider, and running/queued provider, and verify all pass
- [ ] 3.4 Add an integration test proving two concurrent guard reservations for the same provider/mode result in exactly one success and one rejection
- [ ] 3.5 Add consumer-side recheck: on consume, re-read persisted enabled/running/cooldown state and skip obsolete or duplicate messages without executing; verify with a duplicate-delivery test

## 4. Manual fetch trigger endpoints

- [ ] 4.1 Add `POST /api/admin/providers/{id}/fetch` (default mode) that calls the guard, persists Queued history, publishes with confirmation, and returns 202 `{jobId, status:"queued"}`; verify happy path with a real broker
- [ ] 4.2 Add `POST /api/admin/providers/{id}/fetch/full` (ThreatFox only) with 400 for unsupported mode and 409 for cooldown/running; verify each status code
- [ ] 4.3 Verify guard rejections map to 409 (disabled, running, cooldown), unknown provider to 404, and unconfirmed publish marks the history row Failed without returning 202

## 5. Scheduler loop

- [ ] 5.1 Replace the idle worker loop with a scheduler `BackgroundService` polling (every ~15 s) for `isEnabled AND nextFetchTime IS NOT NULL AND nextFetchTime <= now` providers, applying guards and publishing scheduled jobs
- [ ] 5.2 Implement `nextFetchTime` advancement from each provider's configured interval, initialization to `now + interval` on first successful manual fetch, and skip when NULL
- [ ] 5.3 Add injectable-clock unit tests covering first start, restart, due/not-due/disabled, settings changes, cooldown block, failure (no fast retry), and duplicate ticks; verify no double publication

## 6. Fetch history lifecycle

- [ ] 6.1 Implement the lifecycle service resolving the history row by `jobId`, transitioning Queued → Running at first execution, and updating `providers.lastFetchStatus` (Running then Success/Failed) and `lastFetchAt` on success
- [ ] 6.2 Verify retries and redeliveries reuse the same logical history row with integration tests against real RabbitMQ
- [ ] 6.3 Verify Success records exact `recordsAdded`/`recordsUpdated` and a rolled-back batch records Failed with zero counters and the named offender error; enforce the FetchHistory state machine invariants

## 7. Error classification and retries

- [ ] 7.1 Add `FetchErrorClassifier` mapping exceptions to Transient (network, timeout, 5xx, temp-DB) vs Terminal (429/quota, invalid-record, auth, integration-conflict) with table-driven unit tests
- [ ] 7.2 Configure MassTransit `UseMessageRetry` with intervals 10 s / 30 s / 1 min (3 retries) and verify transient failures retry with those delays and then route to `_error` with the history row Failed
- [ ] 7.3 Make terminal failures throw a non-retryable exception so 429, invalid-record, and conflict cases fail after exactly one attempt with a Failed history row and zero IOC writes; verify with integration tests

## 8. Seam for provider adapters

- [ ] 8.1 Add `IProviderFetchExecutor` abstraction (typed result or typed exceptions for quota/parse/conflict) and a fake implementation for tests; verify the consumer calls it within the lifecycle shell
- [ ] 8.2 Wire MassTransit consumer in the Worker host and verify an end-to-end fake-provider fetch transitions history Queued → Running → Success with correct counters and provider status

## 9. Verification and documentation

- [ ] 9.1 Verify the full clean build, unit tests, and integration tests pass (broker integration against real RabbitMQ)
- [ ] 9.2 Confirm no new `.env` keys are required and document the RabbitMQ topology, retry schedule, and stuck-row recovery in the operator notes
