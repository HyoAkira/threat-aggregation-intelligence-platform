# ioc-query-responses Specification

## Purpose

Defines the single source-attributed response contract shared by every IOC query endpoint (search, detail, browse, relations): how rows are grouped, how timestamps aggregate, how provider-specific fields and tags stay attributed, and how null/zero/false values and pagination are represented.

## Requirements

### Requirement: IOC groups aggregate row data by normalized identity
A query result SHALL present matching provider rows as one IOC group keyed by `(iocType, normalized iocValue)`. The group SHALL expose the normalized value, its type token, the nullable full `urlHost` and registrable `parentDomain` derived from that value, and a `sources` collection containing one entry per matching provider row. Group-level `firstSeenUtc` and `lastSeenUtc` SHALL be the MIN and MAX of the returned source rows' `firstSeenUtc` and `lastSeenUtc` respectively. Sources SHALL NOT be merged by provider: a provider may appear more than once when its rows differ by explicit `port`. The empty-string, zero, and false values stored by ingestion SHALL be preserved, never coerced to null.

#### Scenario: Equivalent records across providers group into one result
- **WHEN** the same normalized URL has one ThreatFox row and one PhishTank row
- **THEN** the result is one group whose `sources` contains both provider entries and whose aggregate first/last seen span both rows

#### Scenario: Port variants remain separate source entries
- **WHEN** one provider stores `1.2.3.4:443` and `1.2.3.4:8443` as two rows
- **THEN** the group's `sources` contains two entries for that provider, one per port, with each port on its own source

#### Scenario: Group aggregation spans all returned sources
- **WHEN** source rows have `firstSeenUtc` 2026-01-01 and 2026-06-01 and `lastSeenUtc` 2026-02-01 and 2026-07-01
- **THEN** the group reports firstSeenUtc 2026-01-01 and lastSeenUtc 2026-07-01

### Requirement: Provider-specific fields and tags stay attributed to their source
Each `sources` entry SHALL carry the fields applicable to its provider (for example ThreatFox `threatType`, `malware`, `threatFoxConfidenceLevel`, and PhishTank `isVerified`, `target`, `phishTankOnline`, and its complete validated `phishTankDetails` JSON) plus the provider row id and details link, its own port, its own first/last seen, and the current tag list for that row. Tags SHALL be returned inside the source entry that supplied them; a group-level tag list SHALL NOT flatten attribution. Provider-inapplicable properties SHALL be omitted from the JSON; an applicable property that is a valid `false`, `0`, empty string, or empty array SHALL be included.

#### Scenario: Typed provider fields remain under the owning source
- **WHEN** a group has a ThreatFox source and a PhishTank source for the same normalized URL
- **THEN** the ThreatFox source carries ThreatFox-only fields and the PhishTank source carries PhishTank-only fields, each tagged list appearing only on its own source

#### Scenario: Stored false, zero, and empty values survive serialization
- **WHEN** a source stores `phishTankOnline=false`, `threatFoxConfidenceLevel=0`, an empty string, or an empty details array
- **THEN** the serialized source retains those values rather than dropping them

### Requirement: Calculated false-positive state per source
Each `sources` entry SHALL include an `isFalsePositive` boolean calculated at query time per provider row as `true` when the row's `updatedAt` is older than 180 days before the current UTC time, and `false` otherwise (including exactly at the boundary). The comparison SHALL use the shared UTC clock. A row whose `updatedAt` is refreshed by a successful later upsert SHALL therefore report `false`.

#### Scenario: Row updated within 180 days is not false positive
- **WHEN** a row's `updatedAt` is 179 days before now
- **THEN** the source entry reports `isFalsePositive=false`

#### Scenario: Row at exactly 180 days is not false positive
- **WHEN** a row's `updatedAt` equals now minus 180 days
- **THEN** the source entry reports `isFalsePositive=false`

#### Scenario: Row older than 180 days is false positive
- **WHEN** a row's `updatedAt` is 181 days before now
- **THEN** the source entry reports `isFalsePositive=true`

### Requirement: Paginated list envelope and page bounds
Every multi-item query endpoint (browse and relations) SHALL return an envelope `{ "items": [...], "page": int, "pageSize": int, "totalCount": int }`. `page` SHALL default to 1 and be at least 1. `pageSize` SHALL default to 20 and be clamped or rejected to the inclusive range 1–100; a request outside the allowed bounds SHALL be rejected with a 400 error describing the constraint. `totalCount` SHALL be the number of distinct result groups matching the request, independent of the requested page. `items` on an empty result SHALL be an empty array with `totalCount` 0, not an error.

#### Scenario: Defaults and bounds on a listing request
- **WHEN** a client requests a listing without page parameters
- **THEN** the envelope returns page 1, pageSize 20, and a totalCount that is stable across pages

#### Scenario: Oversized page is rejected
- **WHEN** a client requests pageSize 101
- **THEN** the response is a 400 error and no items are returned

#### Scenario: Empty filtered listing returns empty items
- **WHEN** a filter matches no groups
- **THEN** the response is 200 with an empty `items` array and `totalCount` 0