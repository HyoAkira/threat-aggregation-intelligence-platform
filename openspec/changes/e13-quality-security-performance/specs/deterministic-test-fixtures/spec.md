## Purpose

Supplies sanitized, deterministic provider samples and reusable test builders so all verification runs without live services or secret data unless a real boundary is explicitly required.

## ADDED Requirements

### Requirement: Sanitized provider fixtures
The repository SHALL contain immutable ThreatFox recent/full and PhishTank fixtures using reserved indicators and non-secret credentials. Together they SHALL cover IPv4, IPv6, Domain, URL, ports, every mapped provider field, null/false/zero/empty fidelity, duplicate native and normalized identities, malformed records, and an invalid final record for rollback verification.

#### Scenario: Fixtures run offline
- **WHEN** provider parser tests consume the committed fixtures
- **THEN** every case runs without network access or real provider keys and produces its documented records or deterministic safe error

### Requirement: Deterministic builders and fakes
Reusable builders SHALL provide injected time, repeatable dataset generation, stable identity/pagination ties, and fake provider/HTTP outcomes for success, timeout, 5xx, 429, corrupt or truncated content, cancellation, duplicates, and invalid records. The same seed and clock SHALL produce logically identical inputs.

#### Scenario: Dataset is reproduced
- **WHEN** two runs use the same generator seed and fixed time
- **THEN** their records, identities, ordering ties, and expected outcomes are identical
