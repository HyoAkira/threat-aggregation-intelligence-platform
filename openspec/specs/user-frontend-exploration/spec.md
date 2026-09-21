# user-frontend-exploration Specification

## Purpose

Allows signed-in users to inspect complete IOC evidence, navigate bounded relations, and browse the grouped dataset with durable filter state.

## Requirements

### Requirement: IOC detail and source evidence
`/iocs/:id` SHALL display the E09 group value/type, aggregate dates, false-positive state, and all source entries with provider-specific metadata, tags, and external detail links. External links SHALL be clearly labelled and opened without granting the destination access to the application window.

#### Scenario: Provider-specific evidence is retained
- **WHEN** a detail contains ThreatFox and PhishTank sources
- **THEN** each provider's fields, tags, and false-positive state appear only on its own source card

### Requirement: Paginated relations
For any IOC detail that carries an applicable relation value, the page SHALL let the user request that relation from the relations endpoint: `urlHost` or `parentDomain` for domain and URL details, and `malware` or `target` for any detail with at least one source whose stored malware or target value is non-empty, including IP details. A malware or target request SHALL represent the endpoint's complete resolved-group source-value set rather than a frontend-selected representative source. Relations SHALL load on demand, honor the API page bounds, and expose empty, loading, error, next, and previous states. IP details SHALL not offer host relations.

#### Scenario: Large relation set stays bounded
- **WHEN** a relation has more than one page
- **THEN** the page renders one page at a time and navigation requests the selected page from the API

#### Scenario: Malware and target relations are offered
- **WHEN** an IP, domain, or URL detail has a non-empty malware or target value
- **THEN** the page offers the matching malware and/or target relation control and loads its page on demand

#### Scenario: Later source enables a metadata relation
- **WHEN** the first displayed source has no target and a later source has target `Amazon`
- **THEN** the page offers the target relation control and its result includes groups sharing any target represented by the detail's sources

#### Scenario: Missing relation value hides its control
- **WHEN** a detail stores no malware or target value
- **THEN** the corresponding relation control is not offered

### Requirement: Grouped browse with URL state
`/browse` SHALL expose `type`, `provider`, `tag`, `malware`, `target`, ordering, direction, page, and page-size controls supported by E09. Active controls SHALL be represented in the URL so reload, back, forward, and a copied link restore the same view. Results SHALL be responsive and SHALL distinguish empty data from request failure.

#### Scenario: Browse state survives navigation
- **WHEN** a user applies filters, advances a page, leaves, and returns with browser Back
- **THEN** the same filters, ordering, and page are restored from the URL

#### Scenario: Malware and target filters are durable
- **WHEN** a user applies a malware or target filter and copies the URL
- **THEN** opening the copied URL restores the same filtered view with exactly one browse request
