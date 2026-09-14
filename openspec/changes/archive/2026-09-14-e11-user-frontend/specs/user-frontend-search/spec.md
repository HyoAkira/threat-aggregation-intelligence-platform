## Purpose

Lets signed-in users submit supported indicators and optional search filters and understand grouped source-attributed results and failures.

## ADDED Requirements

### Requirement: Exact IOC search form
`/search` SHALL provide one required IOC input and optional tag/provider filters matching the E09 search contract. The user SHALL NOT choose an IOC type. Empty input SHALL be rejected locally; all other normalization decisions SHALL use the API response.

#### Scenario: Supported IOC is searched without type selection
- **WHEN** a user submits an IPv4, IPv6, domain, or HTTP(S) URL
- **THEN** the frontend sends it as `q` and displays the returned grouped result

### Requirement: Search states and errors
The page SHALL show a pending state while the request runs, a distinct invalid-input message for 400, a not-found state for 404, and a retryable generic error with correlation ID for unexpected failures. It SHALL preserve the submitted query and filters when showing an error.

#### Scenario: Invalid and absent indicators differ
- **WHEN** one request returns 400 and another returns 404
- **THEN** the page explains invalid syntax separately from a valid IOC that is not present

### Requirement: Source-attributed result summary
A successful result SHALL display the normalized value/type, aggregate dates, and every returned source with provider name, port, tags, and applicable typed fields. Null properties SHALL not produce empty labels; valid false, zero, empty string, and empty details values SHALL remain understandable.

#### Scenario: Multiple sources remain separate
- **WHEN** E09 returns two providers and two port variants
- **THEN** the page renders one group summary with each source as a separate labelled source entry
