# provider-record-validation Specification

## Purpose

Defines the canonical record accepted from provider adapters and validates it deterministically before E08 performs atomic upsert, while reporting the first failure without exposing sensitive data.

## Requirements

### Requirement: Canonical provider-record contract
Each incoming record SHALL contain a positive provider ID, a non-blank provider-record ID of at most 100 characters, a non-blank raw IOC input, an optional declared IOC type, a UTC first-seen timestamp, an optional UTC last-seen timestamp, an optional HTTP(S) provider-details link of at most 2048 characters, optional common metadata, a tag collection, and an optional opaque provider payload. Validation SHALL produce a normalized record rather than mutating the input.

#### Scenario: Valid canonical record
- **WHEN** an incoming record contains valid identity, IOC, timestamp, link, metadata, and tag fields
- **THEN** validation produces a new record containing the normalized IOC identity and normalized tags

#### Scenario: Invalid canonical identity
- **WHEN** provider ID is non-positive or provider-record ID is blank or longer than 100 characters
- **THEN** validation fails with the corresponding identity error

### Requirement: Common validation order
The system SHALL validate records in input order and stop at the first invalid record. Within one record it SHALL validate, in order: provider identity, raw IOC presence, IOC normalization and declared-type agreement, UTC timestamps, provider-details link, tags, and the provider-specific validator.

#### Scenario: Valid record
- **WHEN** a record passes all validation rules
- **THEN** it is accepted for ingestion

#### Scenario: Invalid record
- **WHEN** a record violates any validation rule
- **THEN** validation stops and returns the first failure according to the documented record and field order

#### Scenario: Declared type disagrees with normalized type
- **WHEN** a record declares Domain but its raw IOC normalizes as Url
- **THEN** validation fails with a type-mismatch error

#### Scenario: Non-UTC timestamp rejected
- **WHEN** a provider timestamp is local or has an unspecified timezone
- **THEN** validation fails with a timestamp error

#### Scenario: Tags are normalized
- **WHEN** a valid record supplies tags containing case or surrounding whitespace differences
- **THEN** tags are trimmed, lowercased, de-duplicated, and blank tags are removed before the record is accepted

### Requirement: Provider-specific validation extension
The pipeline SHALL invoke exactly one registered validator for the record's provider after common validation succeeds. A missing validator SHALL fail closed. E06 and E07 SHALL supply the concrete ThreatFox and PhishTank payload rules respectively.

#### Scenario: Provider-specific validation succeeds
- **WHEN** common validation succeeds and the registered provider validator accepts the payload
- **THEN** the normalized record is accepted

#### Scenario: Provider-specific validation fails
- **WHEN** common validation succeeds and the registered provider validator rejects the payload
- **THEN** the pipeline returns that provider-specific failure for the current record

#### Scenario: Provider validator is missing
- **WHEN** common validation succeeds but no validator is registered for the provider
- **THEN** validation fails with a stable unsupported-provider error

### Requirement: Secret safety
Each validation failure SHALL contain a one-based input record index, provider ID, provider-record ID truncated to 100 characters, stable error code, field name, and safe message of at most 512 characters. It SHALL NOT contain the raw IOC, opaque provider payload, credentials, authentication headers, or a URL containing user information.

#### Scenario: Failure is safe
- **WHEN** a validation failure is reported
- **THEN** the structured fields identify the record and cause without revealing any prohibited content

### Requirement: Transaction ownership remains outside validation
Validation SHALL perform no database writes and SHALL not create or update fetch history. E08 owns whole-batch rollback, counters, and terminal history state after consuming validation results.

#### Scenario: Validation failure has no persistence side effect
- **WHEN** any provider record fails validation
- **THEN** the validator returns a failure without writing IOC, tag, provider, or fetch-history data
