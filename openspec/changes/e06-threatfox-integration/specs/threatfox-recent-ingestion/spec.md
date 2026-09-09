## Purpose

Streams and parses the ThreatFox recent CSV export into normalized provider records for the supported domain, URL, and `ip:port` IOC types, without writing to the database.

## ADDED Requirements

### Requirement: Streaming CSV parse
The system SHALL parse the ThreatFox recent export CSV as a stream of rows and produce one normalized provider record per accepted row, without buffering the entire feed in memory.

#### Scenario: Feed parsed as a stream
- **WHEN** the recent export CSV is ingested
- **THEN** the system yields normalized provider records row by row without loading the whole file at once

### Requirement: Supported IOC type mapping
The system SHALL map ThreatFox recent rows to the supported IOC types: domains, URLs, and `ip:port` endpoints. Each row SHALL be normalized through the shared E03 normalization service and carry the resulting normalized identity, type, and explicit port where present.

#### Scenario: Domain row
- **WHEN** a recent row is a domain
- **THEN** the record is normalized as a Domain IOC with the canonical value

#### Scenario: URL row
- **WHEN** a recent row is a URL
- **THEN** the record is normalized as a URL IOC, preserving its path and query

#### Scenario: ip:port row
- **WHEN** a recent row is an IP endpoint with a port
- **THEN** the record is normalized as an IP IOC with the address and port stored separately

### Requirement: Malformed row handling with record context
The system SHALL reject a malformed recent row and report a structured failure that identifies the offending row and reason, while continuing to surface it in a form E08's whole-batch abort can consume. A malformed row SHALL NOT be silently dropped.

#### Scenario: Malformed row rejected
- **WHEN** a recent row cannot be normalized or fails validation
- **THEN** the system reports a structured failure naming the row and cause
