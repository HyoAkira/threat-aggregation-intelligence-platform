## Purpose

Streams and parses the ThreatFox recent JSON export into normalized provider records for the supported domain, URL, and `ip:port` IOC types, without writing to the database.

## ADDED Requirements
### Requirement: Streaming JSON parse

The system SHALL parse the ThreatFox recent export JSON as a stream of records and produce one normalized provider record per accepted record, without buffering the entire feed in memory.

#### Scenario: Feed parsed as a stream
- **WHEN** the recent export JSON is ingested
- **THEN** the system yields normalized provider records record by record without loading the whole file at once

### Requirement: Supported IOC type mapping
The system SHALL map ThreatFox recent records to the supported IOC types: domains, URLs, and `ip:port` endpoints. Each record SHALL be normalized through the shared E03 normalization service and carry the resulting normalized identity, type, and explicit port where present.

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

The system SHALL reject a malformed recent record and report a structured failure that identifies the offending record and reason, while continuing to surface it in a form E08's whole-batch abort can consume. A malformed record SHALL NOT be silently dropped.

#### Scenario: Malformed record rejected
- **WHEN** a recent record cannot be normalized or fails validation
- **THEN** the system reports a structured failure naming the record and cause
