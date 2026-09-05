## MODIFIED Requirements

### Requirement: ThreatFox raw responses are stored in a typed table

The system SHALL store ThreatFox API response entries in a table named
`ThreatFox` under the `providers` schema. Each row represents a single IOC
entry from the API response, with typed columns for every field in the
response: `IocId` (int), `IocValue` (nvarchar(500)), `IocType` (nvarchar(50)),
`ThreatType` (nvarchar(100)), `Malware` (nvarchar(200)), `MalwareAlias`
(nvarchar(500), nullable), `MalwarePrintable` (nvarchar(200)), `FirstSeenUtc`
(datetime2), `LastSeenUtc` (datetime2, nullable), `ConfidenceLevel` (int),
`IsCompromised` (bit), `Reference` (nvarchar(max), nullable), `Tags`
(nvarchar(max), nullable), `Anonymous` (int), `Reporter` (nvarchar(200)).

#### Scenario: ThreatFox response entries with long reference URLs are persisted
- **WHEN** a ThreatFox API response contains an IOC entry with a Reference URL exceeding 1000 characters
- **THEN** the entry is inserted as a row in `providers.ThreatFox`
- **AND** the full Reference URL is stored without truncation
- **AND** all other typed columns are stored correctly