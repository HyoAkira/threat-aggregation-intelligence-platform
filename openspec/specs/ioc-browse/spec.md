# ioc-browse Specification

## Purpose

Defines the grouped browse endpoint: filtered, ordered, paginated discovery of IOC groups over `type`, `provider`, normalized `tag`, and first/last-seen UTC ranges, with deterministic aggregate ordering and total counts.

## Requirements

### Requirement: Filtered grouped browse
`GET /api/iocs` SHALL return the shared paginated envelope of IOC groups matching all supplied filters, with filters composing conjunctively:
- `type` SHALL be one of `ipv4`, `ipv6`, `domain`, `url` and SHALL select groups of that type.
- `provider` SHALL be the numeric provider id and SHALL select groups having at least one source row from that provider.
- `tag` SHALL be normalized (trimmed and lowercased) and SHALL select groups having at least one source row that carries the tag; returned groups keep the tag under the supplying source.
- `firstSeenFrom`/`firstSeenTo` and `lastSeenFrom`/`lastSeenTo` SHALL be ISO 8601 UTC instants and SHALL bound the group's aggregate `firstSeenUtc` (MIN) and `lastSeenUtc` (MAX) inclusively.
An invalid enum value, malformed date, or non-integer provider SHALL return 400. A filter that matches nothing SHALL return an empty `items` array with `totalCount` 0.

#### Scenario: Filters compose
- **WHEN** a request filters by `type=url`, `provider=1`, and `tag=phishing`
- **THEN** the response contains only URL groups from provider 1 having at least one source tagged `phishing`

#### Scenario: Tag filter keeps source attribution
- **WHEN** a group has one source tagged `malware` and another untagged
- **THEN** under a `tag=malware` filter the group appears and the returned sources show the tag only on the supplying source

#### Scenario: Datetime range bounds the aggregate
- **WHEN** a request sets `lastSeenFrom` and `lastSeenTo`
- **THEN** only groups whose aggregate MAX lastSeenUtc falls within the inclusive range are returned

#### Scenario: Invalid filter value returns 400
- **WHEN** a request passes `type=email` or a non-date `lastSeenFrom`
- **THEN** the endpoint returns a 400 invalid-input problem detail

### Requirement: Deterministic aggregate ordering
Browse results SHALL be ordered by the requested `orderBy` field among the allowed set `lastSeenUtc` (group MAX), `firstSeenUtc` (group MIN), `iocType`, and `iocValue`, with `dir` of `asc` or `desc`. The default SHALL be `orderBy=lastSeenUtc&dir=desc`. Rows or groups with a NULL sort value SHALL sort last regardless of direction. Ordering SHALL be deterministic across pages and requests: after the primary key, groups SHALL be tie-broken by `iocType` ascending then `iocValue` ascending (ordinal comparison of the normalized value). An `orderBy` outside the allowed set SHALL return 400.

#### Scenario: Default ordering is newest last-seen first
- **WHEN** a client requests browse without order parameters
- **THEN** groups are returned newest-by-aggregate-lastSeen first, with all-null-lastSeen groups last

#### Scenario: NULL aggregate sorts last in both directions
- **WHEN** a client requests `dir=asc` and then `dir=desc`
- **THEN** groups whose sources all have NULL lastSeenUtc appear after all dated groups in both orderings

#### Scenario: Equal aggregate values order deterministically
- **WHEN** two groups share the same aggregate lastSeenUtc
- **THEN** the lower `iocType` then lower `iocValue` group is returned first and page boundaries never split the pair inconsistently

#### Scenario: Disallowed sort field is rejected
- **WHEN** a client requests `orderBy=providerRecordId`
- **THEN** the endpoint returns a 400 invalid-input problem detail