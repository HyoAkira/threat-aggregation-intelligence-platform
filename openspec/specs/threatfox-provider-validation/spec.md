# threatfox-provider-validation Specification

## Purpose

Implements the ThreatFox-specific provider validator on the E03 provider-specific validation extension point, enforcing ThreatFox payload rules and failing closed when a record is not a ThreatFox record.

## Requirements

### Requirement: ThreatFox payload validation
The system SHALL validate ThreatFox-specific payload rules for ThreatFox records after E03 common validation succeeds. Valid ThreatFox records SHALL be accepted for ingestion.

#### Scenario: Valid ThreatFox record
- **WHEN** a ThreatFox record passes common validation and all ThreatFox payload rules
- **THEN** the record is accepted for ingestion

#### Scenario: Invalid ThreatFox payload
- **WHEN** a ThreatFox record violates a ThreatFox payload rule
- **THEN** validation fails with a structured, safe failure for the current record

### Requirement: Fail-closed on non-ThreatFox records
The system SHALL reject a record as unsupported when the registered validator is not the ThreatFox validator, without accepting it.

#### Scenario: Non-ThreatFox record rejected
- **WHEN** a record for a different provider is passed to the ThreatFox validation path
- **THEN** the record is rejected with a stable unsupported-provider failure

### Requirement: Failure safety
ThreatFox validation failures SHALL conform to the E03 failure contract: one-based record index, provider ID, truncated provider-record ID, stable error code, field name, and a safe message, with no raw IOC, opaque payload, credentials, or credential-bearing URLs.

#### Scenario: Failure is safe
- **WHEN** a ThreatFox validation failure is reported
- **THEN** it carries only the sanctioned structured fields and no prohibited content