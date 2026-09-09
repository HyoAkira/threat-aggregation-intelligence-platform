## Purpose

Parses and maps the streamed PhishTank full feed into normalized provider records without whole-file buffering, preserves every applicable field including the complete `phishTankDetails` JSON, and integrates the parsed output through the shared atomic transaction and history lifecycle.

## ADDED Requirements

### Requirement: Streaming parse without whole-file buffering
The system SHALL parse the full PhishTank feed as a stream of records without materializing the entire feed in memory, and SHALL keep additional resident memory within a documented bound (no more than 256 MiB) when processing a fixture at least twice the sampled full-feed size.

#### Scenario: Large fixture stays within memory bound
- **WHEN** a fixture at least twice the current sampled full-feed size is parsed
- **THEN** additional resident memory does not exceed 256 MiB, with the threshold recalibrated against recorded evidence if the real feed requires it

#### Scenario: Corrupt or truncated feed fails cleanly
- **WHEN** the gzip stream or JSON content is corrupt or truncated
- **THEN** parsing fails cleanly without leaving partial provider records or writing to the database

### Requirement: Failure taxonomy
The system SHALL distinguish transport, decompression, JSON-parsing, and record-validation failures so each is handled and reported distinctly.

#### Scenario: Each failure class is identifiable
- **WHEN** a fetch fails during transport, decompression, JSON parsing, or record validation
- **THEN** the reported error identifies which class failed and carries the relevant context

#### Scenario: Invalid record carries record context
- **WHEN** an individual record fails validation
- **THEN** the error identifies the offending record and reason without exposing secrets

### Requirement: PhishTank record mapping
The system SHALL map each valid record to a normalized provider record: `phish_id` as the provider record ID, `phish_detail_url` as the provider details link, the URL normalized per the shared normalization rules, `submission_time` as `firstSeenUtc`, `verification_time` as `lastSeenUtc`, and `verified`, `online`, and `target` as typed fields. Valid `false`, `0`, and empty values SHALL be retained.

#### Scenario: Golden record preserves every value
- **WHEN** a record containing explicit false, zero, and empty values is mapped
- **THEN** the provider record retains every applicable value, including false, zero, and empty

#### Scenario: Detail link and record ID are preserved
- **WHEN** a record is mapped
- **THEN** the provider record carries the `phish_id` and the `phish_detail_url`

### Requirement: phishTankDetails preservation
The system SHALL serialize each record's network detail objects into a validated `phishTankDetails` JSON that round-trips every detail field (`ipAddress`, `cidrBlock`, `announcingNetwork`, `rir`, `detailTime`) for every array element.

#### Scenario: Details JSON round-trips
- **WHEN** a record's detail array is stored as `phishTankDetails`
- **THEN** deserializing the stored JSON yields exactly the original fields for every detail object

#### Scenario: Stored details pass JSON validation
- **WHEN** `phishTankDetails` is persisted
- **THEN** the value is valid JSON per the database check constraint

### Requirement: Contextual IPs are not standalone IOCs
IP addresses appearing only inside `phishTankDetails` SHALL NOT become IOC rows.

#### Scenario: Detail IPs do not create IOC rows
- **WHEN** a record with detail IP addresses is ingested
- **THEN** no separate IOC row is created for those addresses and they exist only within `phishTankDetails`

### Requirement: End-to-end atomic pipeline integration
The system SHALL execute the parsed provider records through the shared transaction and history lifecycle: a valid feed SHALL commit exactly once with correct added/updated counters, and any bad record (including a bad final record) SHALL roll back the entire batch with a Failed history row and zero counters.

#### Scenario: Valid fixture commits once
- **WHEN** a valid PhishTank fixture is fetched end-to-end
- **THEN** the records commit once and the history row reports the exact committed counters

#### Scenario: Bad final record rolls back the batch
- **WHEN** the last record of a feed is invalid
- **THEN** the entire IOC batch is rolled back, no partial writes remain, and the history row is Failed with zero added and updated records and the offender named