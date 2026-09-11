## Purpose

Defines clean-checkout, merge-blocking quality checks that reproduce locally and retain enough evidence to diagnose failures and approve releases.

## ADDED Requirements

### Requirement: Clean-checkout required checks
CI SHALL pin supported .NET and Node toolchains and run backend restore/build with warnings as errors, formatting verification, unit tests, real SQL/RabbitMQ integration tests, frontend reproducible install, formatting, lint, component/flow tests, production build, dependency audits, strict OpenSpec validation, and every Dockerfile present in E13 scope. Any failed command SHALL fail its required check.

#### Scenario: Required command fails
- **WHEN** any build, test, validation, audit, or scoped container command exits unsuccessfully
- **THEN** the CI workflow fails and downstream release readiness cannot pass

### Requirement: Isolated CI services and evidence
CI SHALL start healthy isolated SQL Server and RabbitMQ services using test-only credentials, unique namespaces, no live provider requests, and no persistent developer state. Caches SHALL be keyed by lockfiles, stale runs SHALL be cancellable, and test/security results plus useful failure diagnostics SHALL be retained without secrets.

#### Scenario: Clean clone runs CI
- **WHEN** the workflow starts from a clean checkout without a developer `.env` or local volumes
- **THEN** all required configuration comes from test-safe CI inputs and service-backed checks can run deterministically

### Requirement: Merge protection is documented and checked
Repository automation SHALL document which workflow checks are required by branch protection and provide a repeatable verification step for that external configuration. Workflow success alone SHALL NOT be presented as merge blocking when branch protection is absent.

#### Scenario: Required-check protection is missing
- **WHEN** release review finds that a mandatory workflow is not enforced on the protected branch
- **THEN** the CI quality gate remains incomplete until the external setting is corrected
