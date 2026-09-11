## 1. Deterministic Test Inputs

- [ ] 1.1 Add sanitized ThreatFox recent/full and PhishTank fixture files covering all documented fields, IOC types, fidelity edge cases, duplicates, malformed records, and an invalid final record, and verify parser tests run offline without real keys.
- [ ] 1.2 Add fixed-clock builders, seeded dataset generation, stable ordering ties, and reusable provider/HTTP outcomes, and verify identical seeds and times produce identical logical inputs and results.

## 2. Critical Rule Evidence

- [ ] 2.1 Create a machine-checkable PRD/OpenSpec rule-to-test manifest and validator, and verify it fails for an unmapped rule or missing named test.
- [ ] 2.2 Inventory and link existing E02-E04 normalization, identity, hashing, validation, constraint, seed, and authentication tests, then add only missing boundary coverage; verify every linked test proves its mapped rule.
- [ ] 2.3 Add fixed-time tests for cooldowns, counters, validation ordering, error classification, and the exact 180-day false-positive boundary, and verify failures identify the affected rule without exposing provider content.

## 3. SQL Server System Harness

- [ ] 3.1 Replace the shared SQL test database with unique per-run or per-worker databases and reliable disposal, and verify parallel suites cannot alter or delete each other's state.
- [ ] 3.2 Add real-SQL checks for documented constraints, indexes, error numbers/names, and representative index usability, and verify no EF InMemory result is accepted as SQL evidence.
- [ ] 3.3 Test empty install, ordered migrations, latest-schema idempotency, supported upgrades, and concurrent deterministic seeding, and verify every path converges on one latest schema and one copy of each seed.

## 4. SQL Persistence Behavior

- [ ] 4.1 Add real-SQL ingestion tests for whole-batch rollback, identity conflicts, repeat/concurrent upserts, counters, timestamps, tags, and attribution, and verify the invalid-final-record case leaves no IOC/tag writes and records a failed zero-count history.
- [ ] 4.2 Add grouped exact, detail, browse, relations, and summary tests for nulls, ties, ordering, source attribution, query bounds, and stable pagination, and verify tied pages remain deterministic and disjoint.
- [ ] 4.3 Add authenticated HTTP tests for admin seed/login, JWT roles, every E09 endpoint, and every E10 endpoint over real SQL, and verify the anonymous/User/Admin matrix and persisted effects match public contracts.

## 5. RabbitMQ System Verification

- [ ] 5.1 Add an isolated real-RabbitMQ harness with unique namespaces, durable topology, confirmed publishing, reconnect checks, and cleanup, and verify concurrent runs do not share queues or job state.
- [ ] 5.2 Test one-at-a-time consumption, duplicate redelivery idempotency, retry ordering, immediate terminal failures, exhausted `_error` routing, and one history per `jobId`, and verify persistence matches each lifecycle outcome.
- [ ] 5.3 Inject bounded test retry delays and add a separate production-configuration contract test, and verify four total executions with production delays fixed at 10, 30, and 60 seconds.

## 6. Frontend Flow Verification

- [ ] 6.1 Add a browser-capable frontend harness with validated intercepted or injected API fixtures for loading, success, empty, 400, 401, 403, 404, 409, delayed, and unexpected failures, and verify repeated fixtures produce identical requests and UI state.
- [ ] 6.2 Cover login, deep-link resume, expiry/logout/cache clearing, role routing, search, browse, history, detail, sources, and relations, and verify their URL, validation, not-found, and grouped-success behavior.
- [ ] 6.3 Cover Admin provider, manual-trigger, history, summary, log, and settings flows, and verify duplicate prevention, failed-save preservation, keyboard/focus behavior, accessible announcements, and hostile text rendering.

## 7. Security Assurance

- [ ] 7.1 Create the versioned threat model and review record for every required trust boundary and control area, and verify each area links evidence and records any residual risk.
- [ ] 7.2 Add pinned dependency, image, repository secret, and artifact scanning with a documented severity rubric, and verify canary secrets and open High/Critical findings fail closed without printing secret values.
- [ ] 7.3 Resolve discovered High/Critical findings and add regression evidence, or record owned time-bounded lower-severity exceptions, and verify the security summary links each finding to its disposition.

## 8. Performance Assurance

- [ ] 8.1 Add deterministic small and full-feed benchmark profiles with dataset fingerprints and recorded environment, distribution, duplication, relation, configuration, and chunk-size metadata, and verify a repeated seed reproduces the workload.
- [ ] 8.2 Measure ingestion, memory, tracked entities, log growth, query latency, logical reads, and representative plans, and verify PhishTank streaming stays at or below 256 MiB for at least twice the sampled feed size.
- [ ] 8.3 Calibrate and approve a checked-in budget manifest plus machine-readable comparisons on the documented reference environment, and verify an exceeded budget fails or produces an explicitly owned capacity-limit record.

## 9. Continuous Integration Gates

- [ ] 9.1 Add pinned GitHub Actions jobs for backend restore/build/format/tests, SQL/RabbitMQ services, frontend install/format/lint/tests/build, audits, strict OpenSpec validation, and E13-scope image builds, and verify all required jobs pass from a clean checkout without developer state.
- [ ] 9.2 Add lockfile-keyed caches, stale-run cancellation, test/security artifacts, and secret-safe diagnostics, and verify a deliberately failing command makes its required check fail with useful evidence.
- [ ] 9.3 Document the required branch-protection check names and a repeatable external-settings verification, and verify release readiness remains incomplete when protection is absent.
