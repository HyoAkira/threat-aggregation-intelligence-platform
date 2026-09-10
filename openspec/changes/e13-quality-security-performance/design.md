## Context

See proposal.md. Existing E02–E04 tests already cover broad normalization, validation, hashing, SQL constraints/seeding, and SQL-backed authentication; E13 must retain and trace that evidence rather than recreate it. Current gaps include provider fixture files/builders, isolated parallel SQL databases, migration upgrades, E08/E09 real-SQL breadth, RabbitMQ integration, browser-capable frontend tests, threat/performance evidence, and CI workflows. E05–E12 remain the owners of feature behavior and feature-local tests.

## Goals / Non-Goals

**Goals:** One rule-to-evidence map, deterministic fixtures, production-equivalent boundary tests, stable pull-request gates, deeper scheduled/release checks, and machine-readable security/performance evidence.

**Non-Goals:** No new product endpoints or workflows, live provider calls in tests, EF InMemory evidence for SQL claims, raw percentage coverage target, production packaging/recovery/runbooks, or silent feature changes discovered during verification.

## Decisions

- Build a verification overlay: inventory existing named tests first, map them to PRD/OpenSpec rule IDs, then add only missing boundary cases. Line coverage remains diagnostic, not completion evidence; percentage-only gating was rejected because it does not prove critical semantics.
- Commit sanitized provider fixtures using reserved IP/domain values and generate large deterministic datasets by seed. CI never calls ThreatFox or PhishTank; live tests were rejected as slow, quota-sensitive, nondeterministic, and secret-dependent.
- Replace the fixed shared SQL test database with unique per-run databases and cleanup in `finally`/fixture disposal. Real SQL Server remains mandatory for constraints, migrations, upserts, query ordering, and API persistence.
- Isolate RabbitMQ runs with unique virtual hosts or endpoint names and job IDs. Use shortened injected retry delays in integration tests while a separate contract test locks production 10/30/60-second delays and four total executions; waiting full production time per CI case was rejected.
- Use a browser-capable frontend component harness with request interception or an injected API client. Feature flows consume the same Zod fixture contracts as production; live backend/provider dependencies were rejected for frontend CI.
- Organize execution into fast unit/component, service-backed SQL/broker/API, and scheduled/release security/performance lanes. Pull requests require stable lanes and a small performance regression check; full workloads and complete security evidence are release gates.
- Store threat findings, rule mappings, budgets, benchmark results, and capacity decisions as machine-readable files with concise Markdown summaries. High/Critical findings fail closed; lower accepted risks require ownership and review dates.
- Calibrate performance budgets on the documented representative environment during E13 and commit the approved manifest before declaring TI-136 complete. Inventing latency/volume numbers during planning was rejected because ProjectDocs defines none.
- Use GitHub Actions under `.github/workflows` as the repository CI implementation, with service containers or Compose for SQL/RabbitMQ. Document required branch-protection checks because repository files cannot enforce that external setting.
- Build API/Worker images and the Vite bundle in E13. E14 extends the gate to its nginx frontend image and full production Compose, avoiding a dependency cycle.
- When testing reveals externally observable defects, update the owning earlier capability artifact before fixing code; E13 records evidence but does not silently redefine behavior.

## Risks / Trade-offs

- [Service-backed checks are slower or flaky] → Isolate namespaces, wait on health, fix clocks/seeds, retain diagnostics, and keep only deterministic lanes merge-blocking.
- [Exact SQL plan assertions are brittle] → Assert indexes, logical reads, and budget trends rather than complete plan text.
- [Performance varies on shared runners] → Use a documented reference environment for release budgets and tolerant small-regression checks for pull requests.
- [Fixture samples miss provider drift] → Keep provenance/schema notes and update fixtures deliberately through reviewed provider changes, never live CI fetches.
- [Security tools report noisy findings] → Pin tool versions, use a severity rubric, and require owned time-bounded exceptions.
- [Branch protection is not repository-controlled] → Make its verification a release checklist item with named required checks.

## Migration Plan

Add fixture and traceability infrastructure first, then migrate SQL isolation and add each missing test lane. Introduce CI jobs initially as observable checks, stabilize them, and only then mark them required. Add security/performance release gates after their baseline evidence and budgets are approved. Rollback may disable a newly unstable gate temporarily only with a tracked owner, reason, and restoration deadline; test evidence files remain versioned.
