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
For domain and URL details, the page SHALL let the user request `urlHost` or `parentDomain` relations from the E09 relations endpoint. Relations SHALL load on demand, honor the API page bounds, and expose empty, loading, error, next, and previous states. IP details SHALL not offer host relations.

#### Scenario: Large relation set stays bounded
- **WHEN** a relation has more than one page
- **THEN** the page renders one page at a time and navigation requests the selected page from the API

### Requirement: Grouped browse with URL state
`/browse` SHALL expose `type`, `provider`, `tag`, ordering, direction, page, and page-size controls supported by E09. Active controls SHALL be represented in the URL so reload, back, forward, and a copied link restore the same view. Results SHALL be responsive and SHALL distinguish empty data from request failure.

#### Scenario: Browse state survives navigation
- **WHEN** a user applies filters, advances a page, leaves, and returns with browser Back
- **THEN** the same filters, ordering, and page are restored from the URL
