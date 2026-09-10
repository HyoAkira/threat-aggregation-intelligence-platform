## Why

The application cannot be released as a supportable full stack until the frontend is packaged, startup ownership is deterministic, recovery is rehearsed, and operators have actionable procedures and release gates. E14 turns the completed feature and quality work into the reproducible release defined by Backlog TI-140 through TI-145.

## What Changes

- Add a production multi-stage frontend image served by nginx with `/api` proxying, SPA fallback, compression, caching, and security headers.
- Finalize a production-like Compose stack with health-gated ordering, durable volumes, restart behavior, least-needed published ports, and documented resource guidance.
- Replace competing API/Worker startup migrations with one explicit migration-and-seed owner and visible failure semantics.
- Add backup, restore, application rollback, schema-compatibility, RabbitMQ durability, `.env`, and log-retention procedures, proven by a disposable restore drill.
- Add an operator runbook for health, provider/cooldown failures, stuck jobs, dead-letter inspection/replay, log access, and secret rotation.
- Add a release-readiness checklist covering P0 closure, acceptance metrics, licenses, browsers, accessibility, known limits, credential replacement, and repository/image secret scans.

## Capabilities

### New Capabilities

- `frontend-production-container`: nginx-hosted SPA delivery, API proxying, routing fallback, caching, compression, and browser security headers.
- `production-compose`: health-gated full-stack orchestration, durable storage, restart behavior, network/port exposure, and resource guidance.
- `deployment-lifecycle`: single-owner database migration/seeding and deterministic empty/existing database startup behavior.
- `recovery-operations`: backup, restore, rollback, failure diagnosis, dead-letter handling, and secret-rotation procedures.
- `release-readiness`: objective release evidence, supported-environment declarations, secret/license checks, and sign-off rules.

### Modified Capabilities

- None.

## Impact

- `frontend`: production build configuration, nginx configuration, and container health behavior.
- Dockerfiles and Compose: frontend service, migration job, internal networking, health dependencies, volumes, restart/resource guidance, and production-like overrides.
- API/Worker startup: migration and seed execution moves to the dedicated deployment lifecycle; application services fail clearly when schema readiness is absent.
- Operational documentation and verification scripts: backup/restore drill, rollback constraints, runbook scenarios, release checklist, and artifact/secret scans.
- Depends on E11/E12 frontend routes, E13 quality gates, and the schema/messaging behavior owned by E02/E05/E08.
