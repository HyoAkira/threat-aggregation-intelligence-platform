## Purpose

Defines objective, reproducible evidence that must pass before a version is declared releasable and identifies the exact deployed and rollback artifacts.

## ADDED Requirements

### Requirement: Release preflight gates
Release preflight SHALL require successful E13 backend/frontend builds, formatting/linting, unit, SQL, RabbitMQ, authenticated API and frontend flow tests, accessibility checks, dependency and image audits, container builds, strict OpenSpec validation, effective Compose validation, and clean-start, upgrade, restore, and rollback smoke checks. Any open P0, Critical, High-security, or failed required check SHALL block sign-off.

#### Scenario: Required evidence fails
- **WHEN** any mandatory gate fails or is missing
- **THEN** the candidate cannot be marked releasable

### Requirement: Product and compliance checklist
The checklist SHALL verify PRD success metrics, both provider integrations, all four IOC types, the latest two stable Chrome/Edge/Firefox releases and current Safari, critical-flow WCAG 2.2 AA and keyboard behavior, root license and third-party notices, documented known limits, replacement of default credentials, and absence of real keys, tokens, or credential-bearing URLs in tracked history and built images.

#### Scenario: Default credential remains
- **WHEN** preflight detects a placeholder or documented default production credential
- **THEN** release is blocked without printing the credential value

#### Scenario: Non-P0 limitation is accepted
- **WHEN** a lower-priority limitation remains
- **THEN** sign-off requires its impact, rationale, owner, and documentation link

### Requirement: Immutable release evidence
Each release record SHALL include version, source revision, image digests, migration list, backup verification, quality/security/performance evidence links, supported environments, sign-offs, known limits, and rollback version. Evidence SHALL be durable and sufficient to reproduce the deployed artifact selection.

#### Scenario: Deployment must be reproduced
- **WHEN** an operator examines the release record later
- **THEN** they can identify exact images, schema transition, configuration template, evidence, and rollback target
