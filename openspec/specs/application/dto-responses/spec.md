# application/dto-responses Specification

## Purpose
Defines the typed response contracts returned by MediatR command handlers. These DTOs give the HTTP API a stable, observable response shape instead of anonymous inline objects, making responses type-safe and testable across application boundaries.

## Requirements

### Requirement: Download dataset responses are typed

Commands that download and persist threat intelligence datasets SHALL return a typed response DTO containing the count of processed records, an informational message, and the UTC timestamp of the operation.

#### Scenario: ThreatFox dataset download returns typed response
- **WHEN** a ThreatFox dataset download completes successfully
- **THEN** the command returns a response with the number of processed IOCs
- **AND** the response includes a message indicating success
- **AND** the response includes the UTC timestamp of the operation

#### Scenario: PhishTank dataset download returns typed response
- **WHEN** a PhishTank dataset download completes successfully
- **THEN** the command returns a response with the number of processed phish entries
- **AND** the response includes a message indicating success
- **AND** the response includes the UTC timestamp of the operation

### Requirement: Recent IOC retrieval returns typed response

The operation that fetches and persists recent ThreatFox IOCs SHALL return a typed response DTO containing the count of processed records, an informational message, and the UTC timestamp of the operation.

#### Scenario: Recent IOCs retrieval returns typed response
- **WHEN** the recent ThreatFox IOCs operation completes successfully
- **THEN** the command returns a response with the number of processed IOCs
- **AND** the response includes a message indicating success
- **AND** the response includes the UTC timestamp of the operation

### Requirement: Response DTOs are immutable records

Response DTOs SHALL be immutable records with read-only properties so they cannot be mutated once created.

#### Scenario: DTO properties cannot be modified after construction
- **WHEN** a response DTO is constructed with a record count, message, and timestamp
- **THEN** all properties are readable
- **AND** the DTO has no public setters
