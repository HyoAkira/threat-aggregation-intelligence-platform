## Purpose

Establishes repeatable security review evidence and a fail-closed release rule for high-impact findings, dependencies, containers, and secret boundaries.

## ADDED Requirements

### Requirement: Versioned threat model and review
Each release candidate SHALL have a versioned review covering authentication and password timing, JWT/authorization, provider URL and redirect SSRF, archive/decompression bounds, stored external text, secrets/configuration, log/error redaction, `.env` read/write prohibition, CORS, dependencies, and container exposure. Every area SHALL record assets, trust boundaries, threats, controls, evidence, and residual risk.

#### Scenario: Review area lacks evidence
- **WHEN** a required threat area has no linked control or verification evidence
- **THEN** security review remains incomplete and release readiness fails

### Requirement: Finding closure policy
Findings SHALL use a documented severity rubric. Any open High or Critical finding SHALL block release. An accepted lower-severity residual risk SHALL record an owner, rationale, mitigation, and review date; fixed high-impact findings SHALL gain regression coverage and link their owning capability change when behavior changed.

#### Scenario: High finding remains open
- **WHEN** release readiness evaluates an unresolved High finding
- **THEN** the security gate fails regardless of other passing checks

### Requirement: Automated security gates
CI SHALL audit .NET and npm dependencies and scan relevant built images and repository artifacts for High/Critical vulnerabilities and known canary secrets. Logs and reports SHALL not print secret values.

#### Scenario: Known secret enters a tracked artifact
- **WHEN** a canary credential appears in source, history under review, or a built image
- **THEN** the security gate fails and identifies the artifact without echoing the full value
