## Why

Feature epics establish behavior, but release confidence requires one traceable verification layer across provider parsing, persistence, messaging, authenticated APIs, frontend flows, security boundaries, and realistic data volumes. E13 closes the gaps in Backlog TI-130 through TI-139 while retaining and extending tests that already prove earlier requirements.

## What Changes

- Add sanitized provider fixtures, deterministic builders, clocks, and fake clients covering valid, boundary, duplicate, and malformed data.
- Complete unit and real-SQL verification of critical domain, migration, transaction, upsert, grouping, and pagination rules.
- Add deterministic real-RabbitMQ worker tests and authenticated API tests against real SQL persistence.
- Add frontend component and critical-flow tests with provider-independent API fixtures.
- Perform and record security threat modeling, dependency review, and regression tests for high-risk input and secret boundaries.
- Establish repeatable ingestion/query performance workloads, budgets, and query-plan evidence.
- Add CI gates for formatting/linting, builds, tests, audits, and container builds from a clean checkout.

## Capabilities

### New Capabilities

- `deterministic-test-fixtures`: sanitized provider samples and reusable deterministic test builders, clocks, datasets, and fakes.
- `critical-rule-verification`: traceability from PRD rules to named boundary-focused automated tests.
- `sqlserver-system-verification`: real-SQL migration, constraint, transaction, upsert, query, and authenticated API evidence.
- `rabbitmq-system-verification`: real-broker topology, retry, redelivery, error-routing, and concurrency evidence.
- `frontend-flow-verification`: deterministic component and User/Admin workflow verification without live providers.
- `security-assurance`: threat-model evidence, security regression coverage, dependency auditing, and residual-risk handling.
- `performance-assurance`: reproducible ingestion/query workloads, measurable budgets, and capacity evidence.
- `ci-quality-gates`: merge-blocking clean-checkout CI checks and retained diagnostic artifacts.

### Modified Capabilities

- None.

## Impact

- `tests/ThreatIntelligence.UnitTests` and `tests/ThreatIntelligence.IntegrationTests`: fixtures, builders, SQL/RabbitMQ/API coverage, and performance harnesses.
- `frontend`: browser-capable component/flow tests and stable API fixtures.
- `.github/workflows` and repository tooling: deterministic CI jobs, audits, container builds, reports, and failure artifacts.
- Security/performance documentation: threat model, accepted risks, budgets, measurements, and capacity limits.
- Product behavior changes only when verification exposes a defect; such fixes remain attributable to the owning feature requirement.
