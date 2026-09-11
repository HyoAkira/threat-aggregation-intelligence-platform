## Purpose

Provides reproducible workload definitions, measurable ingestion/query budgets, and explicit capacity decisions for release-scale operation.

## ADDED Requirements

### Requirement: Versioned representative workloads
Performance verification SHALL define deterministic small and full-feed profiles recording their dataset fingerprint, type/provider/tag/port distribution, duplicate/update ratio, relation fan-out, runtime and SQL versions, hardware/resources, configuration, and chunk size.

#### Scenario: Benchmark is reproduced
- **WHEN** an operator reruns a profile with its recorded environment and seed
- **THEN** the generated workload fingerprint and measurement dimensions match the published run

### Requirement: Ingestion and query measurements
Measurements SHALL include ingestion wall time and throughput, peak process memory, peak tracked entities, transaction-log growth, and exact/detail/browse/relations/summary latency plus SQL logical reads and representative plans. PhishTank streaming SHALL be verified at no more than 256 MiB peak process memory for at least twice the sampled feed size.

#### Scenario: Full-feed profile completes
- **WHEN** the full-feed benchmark runs
- **THEN** its machine-readable result contains every required ingestion, resource, query, and environment measurement

### Requirement: Approved budgets or explicit capacity limit
A checked-in budget manifest and machine-readable comparison result SHALL exist before release. Missing evidence or a regression beyond an approved budget SHALL fail the performance gate unless a documented capacity limit records the measured limit, impact, owner, and remediation milestone. Full profiles SHALL run as scheduled/release checks; stable small regressions SHALL run for pull requests.

#### Scenario: Budget is exceeded
- **WHEN** a required metric exceeds its approved threshold
- **THEN** release fails or is explicitly marked capacity-limited with owned remediation rather than silently passing
