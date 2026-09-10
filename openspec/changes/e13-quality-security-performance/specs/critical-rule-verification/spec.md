## Purpose

Makes release-critical product rules traceable to named automated tests instead of relying on a percentage coverage target or informal confidence.

## ADDED Requirements

### Requirement: PRD-to-test traceability
The repository SHALL maintain a machine-checkable manifest mapping every critical FR-001 through FR-012 rule, provider cadence/age boundary, identity rule, counter definition, and retry decision to one or more named automated tests. Existing tests SHALL count when they directly prove the rule. CI SHALL fail when a required rule is unmapped or its named test cannot be found.

#### Scenario: Mapped test is removed
- **WHEN** a referenced test is renamed or deleted without updating evidence
- **THEN** traceability validation fails and identifies the affected rule ID

### Requirement: Boundary-focused rule tests
Critical-rule tests SHALL use injected time and explicit boundary values for normalization, identity/hashing, validation order, provider cooldowns, the 180-day false-positive rule, lifecycle counters, and error classification. Failures SHALL identify the violated rule without exposing raw secret-bearing provider content.

#### Scenario: False-positive age boundary
- **WHEN** one IOC is exactly 180 days old and another is older than 180 days at the fixed evaluation time
- **THEN** only the older IOC is marked false positive, and a later current observation clears that state
