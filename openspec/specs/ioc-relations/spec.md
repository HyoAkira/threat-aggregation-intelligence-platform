# ioc-relations Specification

## Purpose

Defines opt-in, bounded queries for an IOC's relations: other IOC groups that share its exact `urlHost` or its registrable `parentDomain`. Relation data is never inlined without limit; it is always returned through a paginated endpoint the caller explicitly requests.

## Requirements

### Requirement: Opt-in paginated relation queries
Relation data SHALL be available only through `GET /api/iocs/{id}/relations`, which the caller opts into by requesting it; the detail endpoint SHALL NOT embed an unbounded inline list of related URLs. The request SHALL require a `relation` parameter of `urlHost`, `parentDomain`, `malware`, or `target` and SHALL return the paginated list envelope of related IOC groups. For `urlHost` and `parentDomain`, a group is related when its stored corresponding host value equals the resolved group's group-level value. For `malware` and `target`, the resolved group contributes every distinct non-empty trimmed, case-insensitive source value for the requested field, and another group is related when any of its source rows has one of those values. The resolved group itself SHALL be excluded from the related items. Page and pageSize SHALL follow the shared envelope bounds (default 20, maximum 100).

#### Scenario: URL groups sharing an exact host are listed
- **WHEN** a client requests `relation=urlHost` for a URL group whose `urlHost` is `evil.example`
- **THEN** the response returns, paginated, every other URL group with `urlHost=evil.example`, ordered deterministically

#### Scenario: Groups sharing a parent domain are listed
- **WHEN** a client requests `relation=parentDomain` for a domain group whose `parentDomain` is `example.co.uk`
- **THEN** the response returns, paginated, the domain and URL groups whose stored `parentDomain` is `example.co.uk`

#### Scenario: Groups sharing a malware family are listed
- **WHEN** a client requests `relation=malware` for a group with a source whose stored `malware` is `Emotet`
- **THEN** the response returns, paginated, every other IOC group with a source whose stored malware matches that value

#### Scenario: Groups sharing a target are listed
- **WHEN** a client requests `relation=target` for a group with a source whose stored `target` is `Amazon`
- **THEN** the response returns, paginated, every other IOC group with a source whose stored target matches that value

#### Scenario: Multiple source values expand the relation family
- **WHEN** the resolved group has sources with malware values `Emotet` and `TrickBot`
- **THEN** a malware relation returns every other group with a source matching either value, without requiring a source to match both

#### Scenario: No inline related URLs on detail
- **WHEN** a client requests the detail endpoint
- **THEN** the response does not include an unbounded related-URL array

### Requirement: Relation matches use stored host columns exactly
Related groups SHALL be resolved by an exact equality match on the stored `urlHost` or `parentDomain` column, using the supporting indexes. Malware and target relations SHALL compare normalized trimmed/case-insensitive stored values using their supporting indexes, and a candidate group qualifies when any source row matches any normalized value supplied by the resolved group. Host relations (`urlHost` and `parentDomain`) apply only to rows whose type carries that column; IP IOC groups, which store no `urlHost` or `parentDomain`, SHALL NOT be related by host queries and a host relation request for an IP group SHALL be rejected with a 400 response explaining that IP groups have no host relations. Malware and target relations SHALL apply to every IOC type, including IP groups.

#### Scenario: IP group relation is rejected
- **WHEN** a client requests a host relation (`urlHost` or `parentDomain`) for an IPv4 IOC group
- **THEN** the endpoint returns a 400 response explaining that IP groups have no host relations

#### Scenario: IP group malware relation is allowed
- **WHEN** a client requests `relation=malware` for an IP IOC group whose stored `malware` is non-empty
- **THEN** the endpoint returns the other IOC groups sharing that malware value rather than rejecting the request

#### Scenario: Apex and subdomain cases resolve by stored parent domain
- **WHEN** `api.example.co.uk` stores parentDomain `example.co.uk` and `example.co.uk` stores parentDomain `example.co.uk`
- **THEN** each group lists the other under `relation=parentDomain`, and neither lists an unrelated suffix host

#### Scenario: Malware matching is case-insensitive
- **WHEN** the resolved group stores `malware=Emotet` and another group stores `malware=emotet`
- **THEN** the two groups are related under `relation=malware`

#### Scenario: Candidate group matches one of several source values
- **WHEN** the resolved group has target values `Amazon` and `Contoso` across its sources and another group has only a Contoso-targeting source
- **THEN** that other group is included under `relation=target`

#### Scenario: Group without the relation value returns an empty listing
- **WHEN** a client requests `relation=target` for a valid group whose stored `target` is NULL or empty
- **THEN** the endpoint returns 200 with empty `items` and `totalCount` 0

### Requirement: Relation responses are deterministic and bounded
Relation listings SHALL be ordered by a stable key so that pagination does not skip or repeat groups, SHALL cap pageSize at 100, and SHALL report `totalCount` equal to the number of related groups. A valid group with no relations SHALL return an empty `items` array with `totalCount` 0, not an error. An id that does not resolve SHALL return 404.

#### Scenario: Unrelated host returns empty listing
- **WHEN** no other group shares the resolved group's urlHost
- **THEN** the endpoint returns 200 with empty items and totalCount 0

#### Scenario: Unknown id returns 404
- **WHEN** a client requests relations for a non-existent id
- **THEN** the endpoint returns a 404 not-found problem detail