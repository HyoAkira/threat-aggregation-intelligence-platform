## ADDED Requirements

### Requirement: Unsupported IOC type records are skipped

The system SHALL skip ThreatFox recent records whose `ioc_type` is not one of the supported types (domain, URL, `ip:port`) instead of failing the batch. Skipped records SHALL NOT be yielded as provider records and SHALL NOT abort the batch, and the system SHALL log a summary of skipped records for the source broken down by `ioc_type`.

#### Scenario: Unsupported IOC type record skipped

- **WHEN** a recent export contains a record with an unsupported `ioc_type` (for example `sha256_hash`) between supported records
- **THEN** the system yields the supported records, skips the unsupported record, and the batch does not fail

#### Scenario: Feed containing only unsupported records yields nothing and succeeds

- **WHEN** every record in a recent export has an unsupported `ioc_type`
- **THEN** the system yields no provider records, logs the skipped records, and the batch does not fail

#### Scenario: Skip counts logged by type

- **WHEN** one or more records are skipped in a source
- **THEN** the system logs a summary of the skipped record count broken down by `ioc_type`

## MODIFIED Requirements

### Requirement: Malformed record handling with record context

The system SHALL reject a malformed recent record — one whose identifier or IOC is blank, whose timestamps are invalid, or which otherwise fails normalization or validation — and report a structured failure that identifies the offending record and reason, while continuing to surface it in a form E08's whole-batch abort can consume. A malformed record SHALL NOT be silently dropped. Records with an unsupported IOC type are not malformed and are skipped per the unsupported-type skipping requirement.

#### Scenario: Malformed record rejected

- **WHEN** a recent record cannot be normalized or fails validation due to malformed content
- **THEN** the system reports a structured failure naming the record and cause
