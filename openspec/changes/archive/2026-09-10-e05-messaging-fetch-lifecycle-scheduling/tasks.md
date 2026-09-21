## 1. Fetch lifecycle

- [x] 1.1 Define the shared `FetchJob` and verify API/Worker golden-message tests (TI-050).
- [x] 1.2 Configure durable MassTransit topology, confirmation, prefetch/concurrency one, and `_error`; verify broker tests (TI-051, TI-055).
- [x] 1.3 Implement atomic reservation and consume-time guards, excluding the current `jobId`; verify cooldown, concurrency, and duplicate tests (TI-052).
- [x] 1.4 Implement default fetch and ThreatFox-only `/fetch/full`; verify 202/400/404/409 responses (TI-053).
- [x] 1.5 Implement due-provider scheduling and interval recomputation with `TimeProvider`; verify first-run, restart, settings, and failure cadence tests (TI-054).
- [x] 1.6 Reuse one history row across publish, attempts, redelivery, and completion; verify states and counters (TI-056).
- [x] 1.7 Retry only network, timeout, 5xx, and temporary DB failures at 10/30/60 seconds; verify every deterministic failure runs once (TI-057, TI-003).
- [x] 1.8 Verify the full build and broker/SQL integration suite passes.
