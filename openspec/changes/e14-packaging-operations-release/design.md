## Context

See proposal.md. Current Compose is a development topology with host-published database, broker, API, and Worker ports and no frontend. API and Worker both invoke `DatabaseInitializer`, so migration/seeding has competing startup owners. Runtime Dockerfiles use mutable base tags; no production overlay, recovery drill, runbook, release checklist, or license/notice artifacts exist. E14 consumes E11/E12 frontend routes and E13 evidence.

## Goals / Non-Goals

**Goals:** Reproducible versioned images, one deterministic migration gate, least-exposed service networking, selective secret injection, verified recovery, safe operator actions, and objective release evidence.

**Non-Goals:** Kubernetes/cloud/HA, zero-downtime deployment, TLS certificate management, automated remote backup scheduling/retention, in-flight RabbitMQ message backup, automatic destructive schema downgrade, UI/API secret editing, or a newly invented registry/RPO/RTO.

## Decisions

- Keep `docker-compose.yml` as the developer topology and add a documented production overlay or dedicated production Compose file. This preserves local debugging while allowing internal-only ports, frontend ingress, and stricter lifecycle policy; mutating the dev topology was rejected because it would remove useful local access.
- Build the frontend in a pinned Node stage with lockfile install and copy only output into an unprivileged nginx runtime. Nginx owns SPA fallback, `/api` proxying, compression, cache rules, security headers, and `/healthz`.
- Assume TLS terminates outside the HTTP-only Compose stack. Do not enable internal HSTS; verify the final CSP against the actual E11/E12 bundle because an aspirational CSP can break runtime scripts/styles.
- Add a dedicated one-shot migrator executable/image as the sole production owner of EF migration plus deterministic provider/admin seeds. Protect it with a SQL application lock and gate API/Worker on successful completion. Leaving dual host migration in place was rejected because startup ordering does not prevent races.
- Remove runtime migration ownership from API/Worker and split configuration binding by host responsibility so each container receives only required values from the one operator-owned `.env`. Broad `env_file` injection was rejected because it exposes unrelated secrets to every service.
- Use named volumes for restart durability, a distinct exported SQL backup location for recovery, and container plus rolling-file logs as documented. A named database volume is not considered a backup.
- Treat SQL backup as authoritative recovery and RabbitMQ messages as non-backed-up transient delivery state. Recreate broker topology, then reconcile database histories using E05 idempotency instead of promising message restoration.
- Use forward-only migrations. Compatible rollbacks swap to retained image digests against current schema; incompatible rollbacks quiesce writers and restore the verified pre-release database. Automatic EF down migration was rejected as unsafe.
- Version release images and retain their digests, source revision, effective Compose configuration, migration list, and rollback target. Mutable `latest` is not sufficient evidence; upstream base digests are captured/updated through review.
- Use the latest two stable Chrome, Edge, and Firefox releases plus current Safari as the initial supported matrix and WCAG 2.2 AA for critical flows. This is a release-planning compatibility baseline absent from ProjectDocs and can be revised explicitly before implementation.
- Require E13 gates before release sign-off. E14 extends container/Compose checks to the frontend image, migrator, clean deployment, upgrade, recovery, and rollback.

## Risks / Trade-offs

- [Compose dependency order does not ensure ongoing dependency availability] → Test reconnect/readiness and document restart behavior.
- [Per-service configuration requires host binding changes] → Introduce explicit host option groups before removing broad env injection.
- [Migration fails after a backup but before services start] → Fail visibly, keep hosts stopped, and use forward-fix or documented restore based on schema state.
- [CSP/cache rules break SPA updates] → Test real hashed assets, entry revalidation, deep links, and supported browsers from the final bundle.
- [Local backup remains on the same host] → State the limitation and leave off-host retention/RPO/RTO to the deployment owner rather than implying durability.
- [Runbook commands can be destructive] → Default to inspection, require explicit target names and quiescence, and separate production confirmation steps.
- [Base-image tags drift] → Pin or record image digests per release and update them only through reviewed dependency changes.

## Migration Plan

Add and test the frontend image and production Compose configuration first. Introduce the migrator while retaining current startup initialization only in non-production verification, then remove API/Worker production ownership after clean/upgrade paths pass. Rehearse backup, restore, compatible rollback, and incompatible rollback in a disposable project. Enable release sign-off only after E13 gates and every E14 drill pass. Rollback uses retained images and the schema compatibility decision tree; volumes are never deleted as part of ordinary rollback.
