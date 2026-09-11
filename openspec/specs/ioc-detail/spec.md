# ioc-detail Specification

## Purpose

Defines the IOC detail endpoint that resolves one internal row id to its full grouped IOC: aggregate timestamps, the complete source-attributed `sources` collection, and clear 404 behavior for unknown ids.

## Requirements

### Requirement: Detail resolves a row id to its grouped IOC
`GET /api/iocs/{id}` SHALL resolve the row with the given internal id and return the IOC group for its `(iocType, normalized iocValue)`, using the shared group response contract: the normalized value and type, nullable `urlHost` and `parentDomain`, aggregate `firstSeenUtc = MIN` and `lastSeenUtc = MAX` over every row in the group, and a `sources` collection with one source entry per matching row, including the resolved row itself. A valid detail response SHALL NOT require the caller to know the normalized value in advance and SHALL include the same-provider port-variant rows as separate source entries.

#### Scenario: Detail returns the whole cross-provider group
- **WHEN** the resolved row's normalized value also has rows from a second provider and a different-port row from the same provider
- **THEN** the response lists all three as source entries with group timestamps spanning all of them

#### Scenario: Detail type follows the row's normalized identity
- **WHEN** the resolved row is a URL IOC
- **THEN** the group exposes the normalized URL value, `iocType=url`, its `urlHost`, and its registrable `parentDomain`

### Requirement: Missing id returns not found
A request whose `{id}` does not correspond to any IOC row SHALL return 404 with the shared ProblemDetails shape. A malformed (non-positive-integer) id SHALL return 400.

#### Scenario: Unknown row id returns 404
- **WHEN** a client requests a detail for an id that does not exist
- **THEN** the endpoint returns a 404 not-found problem detail

#### Scenario: Non-numeric id returns 400
- **WHEN** a client requests a detail for a non-integer id
- **THEN** the endpoint returns a 400 invalid-input problem detail