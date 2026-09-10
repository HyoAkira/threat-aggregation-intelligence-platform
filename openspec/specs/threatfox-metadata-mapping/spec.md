# threatfox-metadata-mapping Specification

## Purpose

Maps ThreatFox source fields into the canonical provider record so every applicable value — including `false`, `0`, and empty values — is retained and attributed to the ThreatFox source.

## Requirements

### Requirement: ThreatFox field mapping
The system SHALL map the ThreatFox native record ID, provider details link, timestamps, threat/malware fields, confidence level, booleans, reporter, tags, and explicit port into the corresponding canonical provider-record fields.

#### Scenario: All fields mapped
- **WHEN** a ThreatFox record carries every applicable field
- **THEN** each field is retained in the provider record at its canonical location

#### Scenario: Native ID and details link
- **WHEN** a ThreatFox record supplies a native ID and details link
- **THEN** both are retained in the provider record

#### Scenario: Timestamps
- **WHEN** a ThreatFox record supplies first-seen and last-seen times
- **THEN** they are retained as UTC first-seen and last-seen values

### Requirement: Value fidelity including false and zero
The system SHALL distinguish valid `false`, `0`, and empty values from absent values. Provider-inapplicable or absent values SHALL remain null, while a valid `false` or `0` from the source SHALL be retained and not coerced to null.

#### Scenario: False retained
- **WHEN** a ThreatFox boolean field has the value `false`
- **THEN** the record retains `false` and does not represent it as null

#### Scenario: Zero retained
- **WHEN** a ThreatFox confidence level is `0`
- **THEN** the record retains `0` and does not represent it as null

#### Scenario: Empty retained
- **WHEN** a source supplies an empty but meaningful value
- **THEN** the record retains it according to the mapping rule

#### Scenario: Absent remains null
- **WHEN** a ThreatFox field is not present in the record
- **THEN** the corresponding record field remains null

### Requirement: Tags and port mapping
The system SHALL normalize the ThreatFox tag collection into the provider record's tag set and store an explicit `ip:port` endpoint's port separately from the normalized IOC value.

#### Scenario: Tags normalized
- **WHEN** a ThreatFox record supplies tags
- **THEN** the tags are trimmed, lowercased, and de-duplicated into the record's tag set

#### Scenario: Port stored separately
- **WHEN** a ThreatFox record is an `ip:port` endpoint
- **THEN** the port is stored separately from the normalized address