## 1. Frontend Production Image

- [ ] 1.1 Add a pinned multi-stage frontend Dockerfile using lockfile installation and a minimal non-root nginx runtime, and verify a reproducible build contains only generated public artifacts.
- [ ] 1.2 Add build-context exclusions and image/environment secret scans, and verify known canary values plus repository `.env` content are absent from the final image.
- [ ] 1.3 Configure `/api` proxying, SPA fallback, missing-asset 404 behavior, `/healthz`, compression, cache policy, and compatible security headers, and verify each behavior against the built container.

## 2. Production Compose Topology

- [ ] 2.1 Add the production overlay or dedicated production Compose topology for frontend, API, Worker, migrator, SQL Server, and RabbitMQ, and verify `docker compose config` succeeds with explicit versioned image references.
- [ ] 2.2 Add health-gated startup, bounded checks, restart and graceful-stop policies, named data/log volumes, and documented resource guidance, and verify a clean stack reaches healthy state in the required order.
- [ ] 2.3 Make only frontend public by default and put diagnostic SQL, AMQP, management, and API ports behind a loopback-only profile, and verify the rendered default configuration exposes no other non-loopback port.
- [ ] 2.4 Bind selective per-service variables from the operator `.env` without mounting it, and verify frontend receives no backend secrets, Worker receives only required keys, and missing/placeholder credentials fail safely.

## 3. Deployment Lifecycle

- [ ] 3.1 Add the one-shot migrator executable/image as the sole production migration and deterministic-seed owner, and verify API and Worker no longer invoke initialization in normal production startup.
- [ ] 3.2 Add a database-owned exclusive application lock and secret-safe migration outcome logging, and verify two simultaneous migrators serialize without duplicate schema or seed changes.
- [ ] 3.3 Gate API and Worker on successful migration, and verify empty install, supported upgrade, current-schema rerun, and migration-failure scenarios produce the specified startup outcomes.
- [ ] 3.4 Document and enforce the forward-only migration policy with pre-release backup requirements and compatibility classification, and verify no rollback procedure automatically runs an EF down migration.

## 4. Backup, Restore, and Rollback

- [ ] 4.1 Implement the timestamped SQL copy-only backup procedure with checksum, restore verification, release/schema metadata, and protected configuration backup guidance, and verify output does not disclose secrets.
- [ ] 4.2 Implement the disposable-project restore drill with consistency, schema, health, and authenticated smoke checks, and verify it cannot overwrite the source deployment without explicit quiescence and target confirmation.
- [ ] 4.3 Document and test RabbitMQ topology recreation plus database-history reconciliation after broker loss, and verify replacement work preserves `jobId` idempotency and does not claim message backup.
- [ ] 4.4 Rehearse compatible image rollback and incompatible database-restore rollback using retained digests, and verify volumes are preserved and `docker compose down -v` is absent from ordinary rollback.

## 5. Operator Runbook

- [ ] 5.1 Write observation, safe-action, verification, and escalation procedures for service health, SQL/RabbitMQ failure, provider errors, cooldown/disabled states, stuck jobs, `_error` replay, logs/correlation IDs, disk pressure, and secret rotation.
- [ ] 5.2 Rehearse every runbook scenario in a disposable environment with an operator checklist, and verify replay preserves the original `jobId`, destructive steps require explicit targets, and an unfamiliar operator can identify escalation conditions.

## 6. Release Readiness

- [ ] 6.1 Add release preflight automation consuming all E13 gates plus E14 image, effective-Compose, clean-start, upgrade, restore, and rollback checks, and verify any missing or failed mandatory result blocks sign-off.
- [ ] 6.2 Add browser-matrix, WCAG 2.2 AA critical-flow, provider/IOC coverage, credential replacement, license, third-party notice, known-limit, and tracked-history/image secret checks, and verify an open P0 or High/Critical security issue blocks release.
- [ ] 6.3 Create the immutable release-record template for version, source revision, image digests, migrations, backup, evidence, environments, approvals, limits, and rollback target, and verify a completed record uniquely reproduces artifact selection.
- [ ] 6.4 Run the complete production-like preflight from a clean checkout and disposable volumes, and verify all required evidence is retained with no mutable `latest` dependency or real secret exposure.
