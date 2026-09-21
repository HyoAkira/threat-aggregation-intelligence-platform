# threatfox-recent-ingestion Specification

## Purpose

Streams and parses the ThreatFox recent JSON export into normalized provider records for the supported domain, URL, and `ip:port` IOC types, without writing to the database.

## Requirements

### Requirement: Streaming JSON parse

The system SHALL parse the ThreatFox recent export JSON as a stream of records and produce one normalized provider record per accepted record, without buffering the entire feed in memory.

#### Scenario: Feed parsed as a stream
- **WHEN** the recent export JSON is ingested
- **THEN** the system yields normalized provider records record by record without loading the whole file at once

### Requirement: Supported IOC type mapping
The system SHALL parse the combined ThreatFox `recent.json` export and map records to the supported IOC types: domains, URLs, and `ip:port` endpoints. Each record SHALL be normalized through the shared E03 normalization service and carry the resulting normalized identity, type, and explicit port where present.

#### Scenario: Domain record
- **WHEN** a recent record is a domain
- **THEN** the record is normalized as a Domain IOC with the canonical value

#### Scenario: URL record
- **WHEN** a recent record is a URL
- **THEN** the record is normalized as a URL IOC, preserving its path and query

#### Scenario: ip:port record
- **WHEN** a recent record is an IP endpoint with a port
- **THEN** the record is normalized as an IP IOC with the address and port stored separately

### Requirement: Malformed record handling with record context

The system SHALL reject a malformed recent record — one whose identifier or IOC is blank, whose timestamps are invalid, or which otherwise fails normalization or validation — and report a structured failure that identifies the offending record and reason, while continuing to surface it in a form E08's whole-batch abort can consume. A malformed record SHALL NOT be silently dropped. Records with an unsupported IOC type are not malformed and are skipped per the unsupported-type skipping requirement.

#### Scenario: Malformed record rejected
- **WHEN** a recent record cannot be normalized or fails validation due to malformed content
- **THEN** the system reports a structured failure naming the record and cause

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