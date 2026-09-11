# threatfox-full-ingestion Specification

## Purpose

Streams and decompresses the ThreatFox full `.json.zip` export entry-by-entry to produce normalized provider records while bounding memory usage and guarding against corrupt or hostile archives.

## Requirements

### Requirement: Streaming decompression
The system SHALL decompress the full export archive incrementally, reading the JSON archive entry as a stream, and produce normalized provider records without loading the whole archive or decompressed file into memory at once.

#### Scenario: Archive streamed
- **WHEN** the full `.json.zip` export is ingested
- **THEN** the system decompresses and parses the contained JSON as a stream without buffering the entire archive in memory

#### Scenario: Bounded memory
- **WHEN** a full export at least twice the current sampled feed size is ingested
- **THEN** additional resident memory stays within the documented bound

### Requirement: Archive structure validation
The system SHALL locate the expected JSON entry inside the archive and reject an archive that does not contain it, reporting a structured failure.

#### Scenario: Expected entry present
- **WHEN** the archive contains the expected JSON entry
- **THEN** the system decompresses and parses that entry

#### Scenario: Expected entry missing
- **WHEN** the archive lacks the expected JSON entry
- **THEN** the system fails with a structured archive error

### Requirement: Archive-bomb and corruption guards
The system SHALL guard against archive bombs (excessive uncompressed size or entry count) and fail cleanly on corrupt or truncated archives without exhausting memory or reading beyond bounds.

#### Scenario: Archive-bomb guard
- **WHEN** an archive declares an unreasonably large uncompressed size or excessive entries
- **THEN** the system rejects it before decompressing fully

#### Scenario: Corrupt or truncated archive
- **WHEN** the archive data is corrupt or truncated
- **THEN** the system fails cleanly with a structured decompression error

#### Scenario: Malformed JSON record
- **WHEN** a parsed full-export record cannot be normalized or validated
- **THEN** the system reports a structured failure naming the record and cause