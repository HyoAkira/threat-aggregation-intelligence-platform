# Threat Intelligence Aggregator — Epic and Task Backlog

## 1. Purpose and planning assumptions

This backlog turns the PRD and Technical Specification into an implementation sequence. The repository currently contains planning documents and a partial `docker-compose.yml`, so estimates assume a greenfield implementation.

- Priority: **P0** required for a usable MVP, **P1** required for the complete product described by the PRD, **P2** hardening or delivery improvement.
- Size: **S** (up to 1 day), **M** (1–3 days), **L** (3–5 days), **XL** (split before sprint commitment).
- A task is complete only when its acceptance criteria, tests, documentation, and operational checks are complete.
- Suggested delivery is in vertical increments: foundation, ingest one provider, search it, add the second provider, then complete operations and UI.
- Estimates are relative planning aids, not commitments.

## 2. Epic summary and recommended order

| Order | Epic                                             | Outcome                                                                  | Priority | Depends on                 |
| ----- | ------------------------------------------------ | ------------------------------------------------------------------------ | -------- | -------------------------- |
| 0     | E00 — Decisions and backlog readiness            | Ambiguities and infrastructure conflicts are resolved                    | P0       | —                          |
| 1     | E01 — Solution and local platform foundation     | API, worker, database, broker, and frontend run locally                  | P0       | E00                        |
| 2     | E02 — Domain model and persistence               | Schema, constraints, seeds, and migrations implement the data contract   | P0       | E01                        |
| 3     | E03 — IOC normalization and validation           | All supported indicators have deterministic identity and search behavior | P0       | E01                        |
| 4     | E04 — Authentication and authorization           | Users can log in and role boundaries are enforced                        | P0       | E02                        |
| 5     | E05 — Messaging, fetch lifecycle, and scheduling | Fetch jobs are guarded, durable, observable, and retry safely            | P0       | E02                        |
| 6     | E06 — ThreatFox integration                      | Recent and full ThreatFox data can be fetched and mapped                 | P0       | E03, E05                   |
| 7     | E07 — PhishTank integration                      | PhishTank data can be fetched and mapped on its 8-hour interval          | P0       | E03, E05                   |
| 8     | E08 — Atomic upsert and deduplication pipeline   | Re-fetches update without duplicates or partial writes                   | P0       | E02, E03, E05              |
| 9     | E09 — IOC query API                              | Authenticated users can search, browse, group, and trace sources         | P0       | E08                        |
| 10    | E10 — Administration API and observability       | Admins can operate and inspect the system                                | P0       | E05, E08                   |
| 11    | E11 — User frontend                              | Users can log in, search, browse, and inspect IOCs                       | P0       | E04, E09                   |
| 12    | E12 — Admin frontend                             | Admin workflows are usable and safe                                      | P0       | E10, E11                   |
| 13    | E13 — Quality, security, and performance         | Critical rules and realistic workloads are verified                      | P0       | All relevant feature epics |
| 14    | E14 — Packaging, operations, and release         | The full product is deployable and supportable                           | P1       | E11–E13                    |

## 3. Detailed backlog

### E00 — Decisions and backlog readiness

**Goal:** Remove contradictions that would otherwise produce rework.

- [ ] **TI-001 — Reconcile runtime and Compose versions** · P0 · S
  - Choose SQL Server 2022 or 2025 and use it consistently.
  - Remove or isolate the test database host-port collision (`1433:1433` on both services).
  - Add the missing RabbitMQ and worker services to the implementation plan.
  - **Acceptance:** the specification and Compose file name the same images, services, ports, and environment keys.
- [x] **TI-002 — Choose messaging implementation** · P0 · S
  - Use MassTransit over RabbitMQ. Api publishes `FetchJob`; the standalone Worker consumes it.
  - Use MassTransit's interval retry and default `_error` queue; do not add a custom retry topology, scheduler, or DLX for the MVP.
  - **Acceptance:** one approach is recorded with topology ownership, serialization, retry, and testing strategy.
- [x] **TI-003 — Resolve HTTP retry ownership** · P0 · S
  - MassTransit owns retries for the whole job; provider HTTP clients do not retry independently.
  - Retry transient network, timeout, 5xx, and temporary database failures after 10 seconds, 30 seconds, and 1 minute. Do not retry 429/quota, authentication, parsing, validation, or provider-record conflicts.
  - **Acceptance:** total attempt semantics and delays are explicit for timeout, connection failure, 5xx, 429, parse error, and invalid record.
- [x] **TI-004 — Resolve fetch record-counter semantics** · P0 · S
  - `recordsAdded` counts newly inserted IOC rows and `recordsUpdated` counts upserts targeting existing rows, including unchanged incoming values. Both are 0 on rollback.
  - **Acceptance:** history counters have one testable definition across PRD, API, and worker.
- [x] **TI-005 — Resolve provider-record identity conflicts** · P0 · M
  - Treat provider record ID as authoritative and allow its row's normalized value to change. Reject the whole fetch if the new normalized identity already belongs to another provider row; never merge automatically.
  - **Acceptance:** conflict policy is documented and covered by an integration test case.
- [x] **TI-006 — Remove application-managed `.env` editing** · P0 · M
  - Operators edit `.env` manually and restart affected services. API/frontend endpoints never read back secrets or modify the file.
  - **Acceptance:** no endpoint can read back or overwrite arbitrary environment keys or files.
- [x] **TI-007 — Define seed defaults and clock rules** · P0 · S
  - Seed enabled ThreatFox as ID 1 with a 5-minute interval and PhishTank as ID 2 with an 8-hour interval. Keep `nextFetchTime` NULL until the first successful manual fetch. Use UTC through .NET `TimeProvider`.
  - **Acceptance:** deterministic seed and time behavior is documented.
- [x] **TI-008 — Define enqueue and logical-job identity** · P0 · M
  - Create a unique Queued history row, publish directly with broker confirmation, return its `jobId`, and reuse that identity for retries and dashboard polling. Mark unconfirmed publish attempts as Failed; accept the small DB-to-broker crash window instead of adding an outbox.
  - Process one message at a time and recheck persisted guards in the consumer; no distributed provider lock is required for the MVP.
  - **Acceptance:** ordinary publish failure, duplicate delivery, queued duplicates, and terminal error handling reuse one logical history where the same `jobId` is delivered.
- [x] **TI-009 — Finalize query and pagination contracts** · P0 · M
  - Exact search requires `q`; optional `tag` and `provider` narrow the matched group's sources. Tag-only discovery uses grouped browse. Invalid input is 400 and a valid absent match is 404.
  - Relations use `GET /api/iocs/{id}/relations?relation=urlHost|parentDomain&page=&pageSize=` and never appear as an unbounded inline detail array. Browse and relations default to page 1/pageSize 20 and reject pageSize above 100.
  - Browse defaults to aggregate `MAX(lastSeenUtc)` descending, always puts NULL aggregates last, and ties by `iocType` then normalized `iocValue` ascending.
  - Groups are `(iocType, iocValue)`; ports remain separate source rows inside the group.
  - **Acceptance:** PRD, API examples, E09 specs, and backlog tasks use this contract.

### E01 — Solution and local platform foundation

**Goal:** Establish a buildable solution and repeatable developer environment.

- [x] **TI-010 — Scaffold .NET solution and projects** · P0 · M
  - Create Domain, Application, Infrastructure, API, Worker, unit-test, and integration-test projects with dependency direction enforced.
  - **Acceptance:** `dotnet build` and the test suite pass in a clean .NET 10 environment; the local host SDK has an environment-specific project-reference failure.
- [x] **TI-011 — Scaffold React/Vite TypeScript frontend** · P0 · M
  - Add routing, query client, linting, formatting, test runner, and production build.
  - **Acceptance:** TanStack Start/Router provides routing; TanStack Query, Zustand, Zod, Radix UI, Jest/ts-jest, linting, and production build are configured and verified.
- [x] **TI-012 — Centralize typed configuration** · P0 · M
  - Bind and validate database, RabbitMQ, JWT, provider, logging, and seed settings at startup.
  - Include `.env.example` with non-secret placeholders.
  - **Acceptance:** missing or invalid required settings fail fast without logging secrets.
  - **Implementation:** `.env.example` is the canonical template; `.env` is never read back by an endpoint or committed.
- [x] **TI-013 — Complete development Compose topology** · P0 · L
  - Add MSSQL, RabbitMQ management, API, worker, volumes, networks, and health checks.
  - Put the test database behind a profile or non-conflicting port.
  - **Acceptance:** `docker compose up --build` reaches healthy status for MSSQL, RabbitMQ, API, and Worker; test SQL Server uses the `test` profile on port 1432.
- [x] **TI-014 — Add baseline diagnostics** · P0 · M
  - Configure Serilog correlation fields, console/file sinks, health endpoints, and startup diagnostics.
  - **Acceptance:** API and Worker `/health` endpoints report dependency-aware readiness; `X-Correlation-ID` is accepted/returned by API requests; logs contain no secrets.
- [x] **TI-015 — Add repository developer workflow** · P1 · S
  - Add README commands for setup, migration, build, test, and teardown plus editor/format conventions.
  - **Acceptance:** a new developer can start the stack using only documented steps.

### E02 — Domain model and persistence

**Goal:** Implement the common provider-attributed data model and database invariants.

- [x] **TI-020 — Implement domain enums and entities** · P0 · M
  - Model Provider, IOC, Tag, FetchHistory, User, Setting, modes, triggers, statuses, and roles.
  - **Acceptance:** invalid enum/domain states cannot be created through normal constructors or commands.
- [x] **TI-021 — Configure EF Core mappings** · P0 · L
  - Map lower-camel plural tables, lengths, nullability, relationships, UTC timestamps, and enum conversions.
  - **Acceptance:** generated schema matches Technical Specification §4.
- [x] **TI-022 — Add database constraints and indexes** · P0 · L
  - Add both unique identities, hash/search/relation/history indexes, port/confidence checks, and JSON validation.
  - Define correct uniqueness behavior for nullable `port` in SQL Server.
  - **Acceptance:** SQL-backed tests prove every stated constraint, including null-port duplicates.
- [x] **TI-023 — Create initial migration and seed providers** · P0 · M
  - Seed ThreatFox and PhishTank idempotently with decided defaults.
  - **Acceptance:** migration succeeds on an empty database and on a restarted environment without duplicate seeds.
- [x] **TI-024 — Implement admin-user seed** · P0 · M
  - Hash the configured password and create the first admin only when no admin exists.
  - **Acceptance:** plaintext is never persisted/logged; restart does not reset credentials or duplicate the account; missing/invalid settings, an existing same-name User, and concurrent startup have deterministic SQL-backed outcomes.
- [x] **TI-025 — Add repository abstractions and transaction boundary** · P0 · L
  - Support grouped reads, identity resolution, history outside the IOC transaction, and unit-of-work behavior.
  - **Acceptance:** transaction tests demonstrate commit and rollback boundaries.
- [x] **TI-026 — Implement SHA-256 value hashing** · P0 · S
  - Persist exactly 32 bytes calculated from a documented UTF-8 normalized-value representation.
  - **Acceptance:** fixed test vectors match and hash lookups verify the original value to detect collision.

### E03 — IOC normalization and validation

**Goal:** Give ingestion and query paths one shared, rigorously tested normalization service.

- [ ] **TI-030 — Implement strict IPv4 normalization** · P0 · M
  - Reject leading-zero/ambiguous input; support optional valid port.
  - **Acceptance:** canonical, invalid, boundary-port, and whitespace cases pass table-driven tests.
- [ ] **TI-031 — Implement strict IPv6 normalization** · P0 · M
  - Produce compressed canonical addresses; parse bracketed port syntax without confusing address colons.
  - **Acceptance:** expanded/compressed equivalents normalize identically and invalid brackets/ports fail clearly.
- [ ] **TI-032 — Implement domain normalization** · P0 · L
  - Lowercase, remove one root dot, validate labels/length, reject schemes/paths/ports/whitespace, and preserve IDN form.
  - **Acceptance:** specification examples and edge cases pass.
- [ ] **TI-033 — Integrate Public Suffix List resolution** · P0 · M
  - Select a library/data update strategy; calculate registrable domains for domain and URL hosts.
  - **Acceptance:** apex, subdomain, multi-level suffix, IP host, localhost-like, and unknown-suffix cases are defined and tested.
- [ ] **TI-034 — Implement URL normalization** · P0 · L
  - Normalize scheme/host, canonicalize IP hosts, strip fragment/default port/root slash, preserve path/query, and retain explicit port separately.
  - **Acceptance:** all rules in §5.1 have positive and negative tests, including URL length.
- [ ] **TI-035 — Implement query auto-classification** · P0 · M
  - Apply IP → URL → domain precedence and return descriptive validation failures.
  - **Acceptance:** ambiguous and malformed examples classify or fail consistently in API and ingestion paths.
- [ ] **TI-036 — Build provider-record validation pipeline** · P0 · M
  - Validate required fields, dates, ranges, normalized values, provider IDs, and provider-specific payloads.
  - **Acceptance:** failure identifies the first offending provider record and reason without exposing secrets.

### E04 — Authentication and authorization

**Goal:** Protect every non-login business endpoint, keep operational health probes anonymous, and enforce User/Admin roles server-side.

- [x] **TI-040 — Implement password hashing service** · P0 · M
  - Use a current password KDF with per-password salt, work-factor configuration, constant-time verification, and upgrade path.
  - **Acceptance:** hash/verify and invalid-input tests pass; stored format carries algorithm parameters.
- [x] **TI-041 — Implement login command and endpoint** · P0 · M
  - Issue 8-hour HS256 JWTs with username, role, expiry, issuer, and audience.
  - **Acceptance:** valid login returns the contract; invalid credentials return indistinguishable 401 responses.
- [x] **TI-042 — Configure authorization policies** · P0 · S
  - Require authentication by default and Admin role for `/api/admin/*`; allow anonymous `/health` and `/health/live` probes.
  - **Acceptance:** integration matrix proves anonymous business request=401, anonymous health probe=success, User-on-admin=403, and Admin=success.
- [x] **TI-043 — Harden token handling** · P1 · M
  - Validate secret length, clock skew, algorithms, issuer/audience, expiration, and safe auth logging.
  - **Acceptance:** expired, tampered, wrong-algorithm, wrong-issuer, and wrong-audience tokens are rejected.

### E05 — Messaging, fetch lifecycle, and scheduling

**Goal:** Reliably move guarded fetch requests from API/scheduler to a bounded worker.

- [ ] **TI-050 — Define and version `FetchJob` contract** · P0 · S
  - Include the persisted logical job/history ID, provider, trigger, mode, and attempt with strict serialization validation.
  - **Acceptance:** API and worker contract tests share golden messages.
- [ ] **TI-051 — Provision durable RabbitMQ topology** · P0 · M
  - Let MassTransit declare the durable exchange/work queue, default `_error` queue, persistent messages, publisher confirmation, prefetch one, and consumer concurrency one.
  - **Acceptance:** topology survives broker restart and an exhausted message arrives in `_error`.
- [ ] **TI-052 — Implement trigger guard service** · P0 · L
  - Atomically reserve an allowed logical fetch, then enforce enabled state, queued/running state, and ThreatFox mode cooldowns before publish and re-check at consume time.
  - **Acceptance:** boundary-time, simultaneous request/tick, publish-failure, and duplicate-consume tests return consistent allow/409/skip decisions without two reservations.
- [ ] **TI-053 — Implement manual fetch publisher endpoints** · P0 · M
  - Support default mode and ThreatFox full mode; validate provider capabilities; return 202 only after confirmed publication.
  - **Acceptance:** correct jobs publish once; unsupported modes return 400; guarded triggers return 409.
- [ ] **TI-054 — Implement scheduler loop** · P0 · L
  - Poll enabled/due providers, apply guards, publish, and advance each provider's configured interval safely.
  - Define initial `nextFetchTime`, restart behavior, failed scheduled-run cadence, and recomputation after enable/disable or interval shorten/lengthen.
  - **Acceptance:** injectable-clock tests cover first start, restart, due/not-due/disabled, settings changes, failure, cooldown cases, and duplicate ticks.
- [ ] **TI-055 — Enforce one-at-a-time fetch execution** · P0 · S
  - Configure MassTransit prefetch and consumer concurrency to one and recheck job/provider guards before execution.
  - **Acceptance:** only one fetch executes at a time and obsolete duplicate messages do not start another fetch.
- [ ] **TI-056 — Implement fetch history lifecycle** · P0 · L
  - Persist one uniquely keyed logical history row across queue reservation and attempts, then transition Queued/Running → Success/Failed with counter/error details (adding a Queued status or equivalent explicit reservation state).
  - **Acceptance:** publish-failure, crash, stale recovery, retry, redelivery, and DLQ tests produce one coherent history record addressable by the message's logical ID.
- [ ] **TI-057 — Implement error classification and retries** · P0 · L
  - Classify transient, 429/quota, invalid-record, integration-conflict, and terminal errors; cap attempts and route terminal jobs to DLQ.
  - **Acceptance:** automated tests prove exactly the decided attempt count and no retry for deterministic failures.
- [ ] **TI-058 — Add configurable fetch concurrency if needed** · P2 · M
  - Deferred beyond the MVP; if added, introduce the distributed provider locking required for multiple simultaneous consumers.

### E06 — ThreatFox integration

**Goal:** Fetch and map supported ThreatFox domains, URLs, and IP endpoints.

- [ ] **TI-060 — Implement authenticated ThreatFox HTTP client** · P0 · M
  - Construct recent/full export URLs without logging the key; use descriptive user agent, timeouts, streaming, and cancellation.
  - **Acceptance:** mocked requests prove URL/mode selection and secret redaction.
- [ ] **TI-061 — Implement recent-export ingestion** · P0 · L
  - Stream, parse, validate, and return normalized provider records for supported domains, URLs, and IP-port records; do not write the database here.
  - **Acceptance:** canned fixtures map all supported types and reject malformed rows with record context.
- [ ] **TI-062 — Implement zipped full-export ingestion** · P0 · L
  - Stream decompression and return provider records without loading the whole archive in memory; validate expected archive entries.
  - **Acceptance:** fixture and corrupt/truncated/archive-bomb guard tests pass.
- [ ] **TI-063 — Map ThreatFox metadata and tags** · P0 · L
  - Map native ID, details link, timestamps, threat/malware fields, confidence, booleans, reporter, tags, and port.
  - **Acceptance:** a golden provider row retains every applicable value including `false` and `0`.
- [ ] **TI-064 — Verify ThreatFox cadence and failure behavior** · P0 · M
  - Cover 5-minute recent and 60-minute full cooldowns, upstream 429/5xx/timeouts, and cancellation.
  - **Acceptance:** integration tests exercise each classification and history outcome.
- [ ] **TI-065 — Integrate ThreatFox adapter with atomic pipeline** · P0 · M · Depends on TI-080–TI-084
  - Execute recent/full adapter output through the shared transaction and history lifecycle.
  - **Acceptance:** an end-to-end fixture fetch commits valid data once and rolls back the entire batch on a bad final record.

### E07 — PhishTank integration

**Goal:** Fetch the full PhishTank database while preserving URL and evidence metadata.

- [ ] **TI-070 — Implement PhishTank HTTP client** · P0 · M
  - Use a descriptive user agent, stream `online-valid.json.gz`, support optional future app-key URL, and set safe timeouts.
  - **Acceptance:** requests identify the application and never log an optional key.
- [ ] **TI-071 — Stream and parse the full JSON feed** · P0 · L
  - Avoid whole-file buffering; distinguish transport, decompression, JSON, and record-validation failures.
  - **Acceptance:** a fixture at least twice the current sampled full-feed size uses no more than 256 MiB additional resident memory; corrupt input fails cleanly. Recalibrate this threshold with recorded evidence if the actual feed requires it.
- [ ] **TI-072 — Map PhishTank records and complete details** · P0 · L
  - Map phish ID/link, URL, submission/verification time, verified, online, target, and all network detail objects.
  - **Acceptance:** validated stored JSON round-trips every detail field and contextual IPs do not become standalone IOCs.
- [ ] **TI-074 — Implement PhishTank 429 behavior** · P0 · S
  - Fail immediately without retry and record a clear operational reason.
  - **Acceptance:** a simulated 429 results in one attempt, Failed status, and zero IOC writes.
- [ ] **TI-075 — Integrate PhishTank adapter with atomic pipeline** · P0 · M · Depends on TI-080–TI-084
  - Execute parsed provider records through the shared transaction and history lifecycle.
  - **Acceptance:** an end-to-end fixture fetch preserves complete details, commits once, and rolls back entirely on a bad final record.

### E08 — Atomic upsert and deduplication pipeline

**Goal:** Commit valid batches exactly once while preserving provider attribution.

- [ ] **TI-080 — Implement identity resolution** · P0 · L
  - Resolve provider/native ID and provider/type/hash/port; compare normalized value after hash lookup.
  - **Acceptance:** new, repeat, cross-provider, port variant, and conflict cases follow the documented policy.
- [ ] **TI-081 — Implement provider-row upsert** · P0 · L
  - Preserve `createdAt` and stored `firstSeenUtc`; refresh `updatedAt`; replace provider metadata and incoming `lastSeenUtc`.
  - **Acceptance:** a re-fetch changes only the fields allowed by the specification.
- [ ] **TI-082 — Implement normalized tag replacement** · P0 · M
  - Trim/lowercase/deduplicate tags and replace associations for each upserted provider row. Cache
    only normalized tag-name/ID pairs across chunks and reference tags by scalar ID.
  - **Acceptance:** removed provider tags disappear, shared tag rows remain, tags retain source attribution,
    and a tag first created in one chunk is reused without error by later chunks.
- [ ] **TI-083 — Implement whole-batch transaction** · P0 · L
  - Process approximately 2,000 records at a time, save each chunk, and clear EF tracking while
    retaining one outer transaction; history remains outside it.
  - **Acceptance:** tracked entity count stays bounded, tags are not duplicated across chunks, and an
    invalid final record rolls back all IOC/tag changes and leaves Failed history with zero committed rows.
- [ ] **TI-084 — Make redelivery idempotent** · P0 · L
  - Handle unique-key races and replay without duplicates or false success counts.
  - **Acceptance:** repeated and concurrent delivery produces the same final dataset as one delivery.
- [ ] **TI-085 — Calculate false-positive state at query time** · P1 · S
  - Compare `updatedAt` with exactly 180 days using the shared UTC clock.
  - **Acceptance:** before/on/after boundary tests pass and reappearance clears the calculated state.

### E09 — IOC query API

**Goal:** Provide index-backed grouped search, detail, relations, and browse APIs.

- [ ] **TI-090 — Define source-attributed response DTOs** · P0 · M
  - Omit null provider-inapplicable properties while retaining valid false/zero/empty values.
  - **Acceptance:** golden JSON contract tests match the documented response.
- [ ] **TI-091 — Implement exact IOC search** · P0 · L
  - Normalize/classify query; group by type/value; handle port-bearing versus bare IP semantics.
  - **Acceptance:** valid match, invalid=400, not-found behavior, cross-provider grouping, and port cases pass.
- [ ] **TI-092 — Implement tag search** · P1 · L
  - Implement the tag search and pagination behavior fixed by TI-009 while returning complete, source-attributed group details.
  - **Acceptance:** response identifies which source supplied each tag without flattening attribution.
- [ ] **TI-093 — Implement IOC detail endpoint** · P0 · L
  - Resolve a row ID to its group, aggregate MIN first-seen/MAX last-seen, and return every source entry.
  - **Acceptance:** missing=404 and separate same-provider port rows remain separate sources.
- [ ] **TI-094 — Implement related host/parent-domain/URL queries** · P1 · L
  - Query exact `urlHost` and `parentDomain`; use the pagination contract fixed by TI-009 to prevent unbounded payloads.
  - **Acceptance:** apex/subdomain/IP-host cases return defined relations using indexes.
- [ ] **TI-095 — Implement grouped browse endpoint** · P0 · L
  - Add type/provider/tag filters, the aggregate null/tie ordering fixed by TI-009, page bounds, and total count.
  - **Acceptance:** filters compose correctly, pagination is deterministic, and `pageSize` cannot exceed 100.
- [ ] **TI-096 — Add RFC 7807 error handling** · P0 · M
  - Map validation, not found, conflict, auth, and unexpected errors with correlation IDs.
  - **Acceptance:** API failures share a safe, consistent ProblemDetails shape.

### E10 — Administration API and observability

**Goal:** Give administrators accurate operational controls and diagnostics.

- [ ] **TI-100 — Implement provider dashboard endpoint** · P0 · M
  - Return status/times, enabled/interval, available modes, and base metadata.
  - **Acceptance:** running/success/failed and never-run providers serialize correctly.
- [ ] **TI-101 — Implement paginated fetch history endpoint** · P0 · M
  - Order newest first and expose safe terminal error details.
  - **Acceptance:** pagination, role gate, and large-error handling tests pass.
- [ ] **TI-102 — Implement grouped database summary** · P0 · M
  - Count distinct normalized type/value groups total and per type, consistent with query grouping.
  - **Acceptance:** shared cross-provider IOCs count once and port variants remain source rows inside the same type/value group.
- [ ] **TI-103 — Implement runtime settings read/update** · P0 · L
  - Validate provider enabled/interval and log level; invalidate caches/apply within 30 seconds.
  - **Acceptance:** invalid updates are atomic 400s; valid updates affect running services without restart.
- [ ] **TI-105 — Implement log-tail endpoint** · P0 · L
  - Parse structured rolling logs, filter by level, enforce count ≤500, newest first, and tolerate rotation/partial lines.
  - **Acceptance:** malformed entries do not fail the request and path traversal is impossible.
- [ ] **TI-106 — Add fetch lifecycle structured events** · P0 · M
  - Log start, guard rejection, success, retry, terminal failure, counts, job/provider/mode/trigger, and duration.
  - **Acceptance:** an operator can correlate one fetch across API, broker, and worker without secret or full IOC leakage.

### E11 — User frontend

**Goal:** Deliver accessible user workflows for threat-indicator discovery.

- [ ] **TI-110 — Establish frontend design system and shell** · P0 · L
  - Create deliberate visual tokens, responsive navigation, loading/empty/error patterns, and accessible Radix primitives.
  - **Acceptance:** core shell works at mobile/desktop widths and keyboard navigation passes a basic audit.
- [ ] **TI-111 — Implement auth state and API client** · P0 · M
  - Add JWT/session handling, bearer injection, logout, expiry handling, query defaults, and ProblemDetails mapping.
  - **Acceptance:** expired/401 sessions return to login and protected data is cleared.
- [ ] **TI-112 — Build login page and route guards** · P0 · M
  - Provide validation, safe error feedback, post-login routing, and User/Admin navigation differences.
  - **Acceptance:** unauthenticated deep links redirect and resume appropriately after login.
- [ ] **TI-113 — Build IOC search page** · P0 · L
  - Add one search input, type/validation feedback, not-found/loading/error states, grouped result summary, and tag search entry.
  - **Acceptance:** supported IOC examples can be pasted and searched without choosing a type.
- [ ] **TI-114 — Build IOC detail page** · P0 · L
  - Show aggregate dates, all provider source cards, provider-specific metadata/tags, false-positive state, and external detail links.
  - **Acceptance:** null fields disappear while false/zero/empty values remain understandable.
- [ ] **TI-115 — Build related IOC presentation** · P1 · M
  - Show host, parent domain, related URL count/list, pagination/cap behavior, and navigation.
  - **Acceptance:** large related sets do not freeze the page or create an unbounded request.
- [ ] **TI-116 — Build browse page** · P0 · L
  - Add type/provider/tag filters, stable pagination/sorting, URL-safe filter state, and responsive results.
  - **Acceptance:** reload/back/forward retain filters and empty/error states are explicit.

### E12 — Admin frontend

**Goal:** Make fetch operations, history, metrics, logs, and settings manageable.

- [ ] **TI-120 — Build provider dashboard** · P0 · L
  - Display status, last/next fetch, enabled state, modes, interval, and cooldown countdown.
  - **Acceptance:** refresh/polling does not duplicate actions or show stale running state indefinitely.
- [ ] **TI-121 — Implement manual fetch controls** · P0 · M
  - Show only supported modes, confirm full fetch where appropriate, disable guarded actions, and surface 202/409 outcomes.
  - **Acceptance:** double-click cannot enqueue duplicate jobs and cooldown reason is visible.
- [ ] **TI-122 — Build fetch history view** · P0 · M
  - Paginate per provider and display trigger/mode/status/timing/count/error.
  - **Acceptance:** long errors are inspectable without breaking layout or executing content.
- [ ] **TI-123 — Build database summary page** · P0 · S
  - Present total and per-type grouped counts with refresh timestamp.
  - **Acceptance:** values match the admin summary API exactly.
- [ ] **TI-124 — Build log viewer** · P0 · M
  - Add level/count controls, safe rendering, refresh, timestamps, and failure states.
  - **Acceptance:** maximum count is enforced client-side and arbitrary log text is never treated as markup.
- [ ] **TI-125 — Build runtime settings page** · P0 · L
  - Edit provider intervals/enabled state and log level with field-level validation and unsaved-change protection.
  - **Acceptance:** failed saves preserve edits; successful changes report when they take effect.

### E13 — Quality, security, and performance

**Goal:** Prove the system’s critical business rules and production risk controls.

- [ ] **TI-130 — Create test fixtures and builders** · P0 · M
  - Add sanitized ThreatFox recent/full and PhishTank fixtures plus factories and fake provider clients.
  - **Acceptance:** fixtures cover every supported IOC type, null/false/zero values, duplicate identities, and bad records.
- [ ] **TI-131 — Complete critical unit-test matrix** · P0 · L
  - Cover normalization, identity, hashing, validation, guard math, false-positive cutoff, and error classification.
  - **Acceptance:** every PRD critical business rule has a named automated test.
- [ ] **TI-132 — Test schema and migration behavior on SQL Server** · P0 · L
  - Cover constraints, indexes, initial/upgrade migrations, seeding, and concurrent seed startup.
  - **Acceptance:** tests run against a real isolated SQL Server and prove expected failure codes for every constraint.
- [ ] **TI-138 — Test transaction, upsert, and grouped-query behavior on SQL Server** · P0 · L
  - Cover atomic rollback, identity conflicts, concurrent upsert, grouping, aggregation, pagination ordering, and representative query plans.
  - **Acceptance:** tests are repeatable and detect partial writes, duplicate identities, unstable pages, and loss of source attribution.
- [ ] **TI-139 — Test authenticated API behavior on real persistence** · P0 · M
  - Cover login/seed cases, authorization matrix, search/detail/browse, and admin endpoints using SQL Server.
  - **Acceptance:** the public API contract passes end-to-end without EF InMemory substitutes.
- [ ] **TI-133 — Complete broker/worker integration tests** · P0 · L
  - Cover publish confirmation, retry delay/cap, redelivery, `_error` routing, logical history reuse, and one-at-a-time consumption.
  - **Acceptance:** tests use a real RabbitMQ instance and are deterministic.
- [ ] **TI-134 — Complete frontend component and flow tests** · P1 · L
  - Cover login, role routing, search, browse filters, detail attribution, manual triggers, and settings errors.
  - **Acceptance:** critical paths run in CI without live provider dependencies.
- [ ] **TI-135 — Run security review** · P0 · L
  - Threat-model auth, SSRF/provider URLs, archive/decompression, stored external text, secrets, logs, `.env` writes, CORS, and dependencies.
  - **Acceptance:** all high findings are fixed; accepted residual risks are documented.
- [ ] **TI-136 — Validate ingestion and query performance** · P1 · L
  - Benchmark realistic full-feed volume, chunk size, memory, transaction-log growth, exact search, browse, summary, and related queries.
  - **Acceptance:** agreed latency/resource targets pass or a documented capacity limit and remediation plan exists.
- [ ] **TI-137 — Add CI quality gates** · P0 · M
  - Build, format/lint, unit/integration tests, frontend build, dependency audit, and container build.
  - **Acceptance:** required checks pass on a clean checkout and failures block merge.

### E14 — Packaging, operations, and release

**Goal:** Ship a reproducible full-stack release with recovery and support guidance.

- [ ] **TI-140 — Build production frontend image** · P1 · M
  - Serve static assets through nginx with `/api` proxy, SPA fallback, caching, security headers, and compression.
  - **Acceptance:** direct navigation to every SPA route works in Compose.
- [ ] **TI-141 — Finalize production-like Compose** · P1 · L
  - Add frontend, health-based dependencies, restart policies, volumes, log access, least-needed ports, and resource guidance.
  - **Acceptance:** full stack starts cleanly, becomes healthy, restarts safely, and exposes only intended services.
- [ ] **TI-142 — Add migration and startup policy** · P0 · M
  - Decide explicit migration job versus controlled app migration; prevent two services racing migrations.
  - **Acceptance:** empty and existing databases upgrade once with visible failure behavior.
- [ ] **TI-143 — Document backup, restore, and rollback** · P1 · M
  - Cover MSSQL data, RabbitMQ durability expectations, `.env`, logs, schema rollback limits, and release rollback.
  - **Acceptance:** a restore drill succeeds using documented commands and a disposable environment.
- [ ] **TI-144 — Write operator runbook** · P1 · M
  - Document health checks, common provider failures, cooldown behavior, stuck Running recovery, DLQ inspection/replay, secret rotation, and log locations.
  - **Acceptance:** an operator unfamiliar with the code can diagnose each rehearsed scenario.
- [ ] **TI-145 — Complete release readiness review** · P0 · M
  - Verify acceptance metrics, license notices, supported browsers, accessibility, known limits, default credentials changed, and no real provider keys in history/images.
  - **Acceptance:** release checklist is signed off and all P0 issues are closed.

## 4. Suggested milestones

### M1 — Executable foundation

Includes E00–E04. The solution builds; dependencies are healthy; schema, normalization, login, and role enforcement are proven by tests.

### M2 — First vertical slice: ThreatFox search

Includes the P0 portions of E05, E06, E08, E09, and E11. An admin can initiate a ThreatFox recent fetch and a user can search and inspect imported IOCs with source attribution.

### M3 — Multi-provider core

Includes E07 plus the P0 browse and user UI work. ThreatFox and PhishTank ingest atomically, equivalent IOCs group across providers, and the principal user experience is complete. This is not the PRD-complete MVP because required administration workflows remain.

### M4 — PRD-complete MVP

Includes E10 and E12. Admins can schedule/trigger safely, inspect history/status/summary/logs, and manage runtime configuration. Conditional secret-editing tasks may be deferred only if TI-006 formally removes that capability from the MVP specification.

### M5 — Release candidate

Includes E13–E14. Tests, performance, security, deployment, recovery, and runbooks meet the release gate.

## 5. Cross-cutting definition of done

Every task must satisfy the applicable items below:

- Acceptance criteria are demonstrated and linked to the PRD story/requirement when applicable.
- Unit tests cover business logic; SQL, broker, or HTTP behavior uses integration/contract tests where mocks would hide risk.
- Errors are classified, safe for users, and correlated in structured logs without secrets or unnecessary IOC payloads.
- Cancellation tokens, timeouts, UTC/injectable time, nullable values, pagination bounds, and concurrent execution are handled explicitly.
- API changes update contracts/examples; configuration changes update `.env.example` and startup validation.
- Database changes include forward migration, indexes/constraints, and an upgrade/recovery note.
- UI work includes keyboard access, visible loading/empty/error states, responsive behavior, and no unsafe HTML rendering.
- The clean build, automated tests, lint/format checks, and relevant Compose health checks pass.

## 6. Initial sprint recommendation

Sprint 1 should include TI-001 through TI-010, TI-012, TI-013, TI-020, TI-021, TI-023, TI-026, and TI-030 through TI-035. Its exit is a backend/platform foundation with resolved contracts, a running local dependency stack, a migrated core model, and fully tested normalization—not completion of Milestone 1. TI-011, TI-022, TI-024, TI-025, TI-036, and TI-040 through TI-043 follow to close M1.

Do not begin both provider adapters in the first sprint. Complete a thin ThreatFox recent-fetch vertical slice through storage and search first; it will expose contract, transaction, scheduling, and UI integration problems before the larger full feeds and PhishTank interval rules increase complexity.
