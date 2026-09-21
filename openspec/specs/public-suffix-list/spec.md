# public-suffix-list Specification

## Purpose

Resolves the registrable `parentDomain` for domain and URL hosts using Public Suffix List rules, enabling correct host and parent-domain relations regardless of suffix depth.

## Requirements

### Requirement: Registrable domain resolution
The system SHALL calculate the registrable domain for a DNS host using a checked-in Public Suffix List snapshot and the PSL prevailing rule. Resolution SHALL be deterministic and SHALL require no runtime network access.

#### Scenario: Apex domain
- **WHEN** an apex such as `example.co.uk` is resolved
- **THEN** the registrable domain is itself

#### Scenario: Subdomain
- **WHEN** `api.example.co.uk` is resolved
- **THEN** the registrable domain is `example.co.uk`

#### Scenario: IP or localhost-like host
- **WHEN** an IP literal or a localhost-like host is resolved
- **THEN** the host is returned as-is (no registrable-domain reduction)

#### Scenario: Unknown suffix
- **WHEN** `a.example.unknown` is resolved and `unknown` is absent from the pinned list
- **THEN** the PSL prevailing `*` rule returns `example.unknown`

#### Scenario: Public suffix alone
- **WHEN** a host consists only of a public suffix such as `co.uk`
- **THEN** resolution reports that no registrable domain exists

### Requirement: Reproducible PSL data lifecycle
The system SHALL use `Nager.PublicSuffix` 3.8.0 with a checked-in PSL snapshot. Repository metadata SHALL identify the snapshot's publicsuffix.org source URL, retrieval date, and SHA-256 checksum. PSL updates SHALL occur through an intentional reviewed snapshot replacement rather than an application-startup download.

#### Scenario: Offline startup
- **WHEN** the application starts without internet access
- **THEN** registrable-domain resolution remains available from the pinned data

#### Scenario: PSL data update
- **WHEN** the checked-in PSL snapshot is replaced
- **THEN** its source, retrieval date, and checksum change are reviewable and all PSL regression tests run against the updated data
