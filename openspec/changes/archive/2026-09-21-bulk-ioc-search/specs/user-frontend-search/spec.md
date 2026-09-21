## MODIFIED Requirements

### Requirement: Exact IOC search form
`/search` SHALL provide a required multiline IOC value editor and optional tag/provider filters matching the E09 search contract. The editor SHALL accept one IOC per line, ignore blank lines for submission, preserve free-form entry, and SHALL NOT require the user to choose an IOC type. A single non-empty line SHALL retain the existing exact-search behavior; two or more non-empty lines SHALL use the bulk-search contract.

#### Scenario: A single supported IOC keeps the existing search flow
- **WHEN** a user submits one non-empty IPv4, IPv6, domain, or HTTP(S) URL line
- **THEN** the frontend sends it as the existing `q` search and displays the returned grouped result

#### Scenario: Supported IOC is searched without type selection
- **WHEN** a user submits an IPv4, IPv6, domain, or HTTP(S) URL
- **THEN** the frontend sends it as `q` and displays the returned grouped result

#### Scenario: Multiple lines use bulk search
- **WHEN** a user submits multiple non-empty IOC lines
- **THEN** the frontend sends the trimmed values in their editor order to the bulk-search endpoint and displays one outcome for each line

#### Scenario: Blank lines are not submitted
- **WHEN** the editor contains blank lines before, between, or after IOC values
- **THEN** the frontend omits those lines from the request and does not render blank result entries

#### Scenario: The user never chooses an IOC type
- **WHEN** the search editor is rendered
- **THEN** no IOC-type selector is displayed and the API remains responsible for classification

### Requirement: Search states and errors
The page SHALL show a pending state while a single or bulk request runs. Single-search 400 and 404 responses SHALL remain distinct. A bulk response SHALL show per-line invalid, not-found, and matched states without treating mixed outcomes as a page-level failure. Unexpected failures SHALL show a retryable generic error with correlation ID, and the page SHALL preserve the submitted lines and filters when showing an error.

#### Scenario: Mixed bulk outcomes remain visible
- **WHEN** a bulk request returns matched, not-found, and invalid items
- **THEN** the page renders those statuses next to the corresponding submitted values and still displays the matched groups

#### Scenario: Invalid and absent indicators differ
- **WHEN** one single-search request returns 400 and another returns 404
- **THEN** the page explains invalid syntax separately from a valid IOC that is not present

#### Scenario: Bulk transport failure is retryable
- **WHEN** the bulk request fails unexpectedly
- **THEN** the page preserves the submitted editor contents, shows the correlation ID when available, and offers retry

#### Scenario: Empty editor is rejected locally
- **WHEN** the editor contains no non-empty lines
- **THEN** the page shows a local validation message and does not call either search endpoint

### Requirement: Source-attributed result summary
A successful result SHALL display the normalized value/type, aggregate dates, and every returned source with provider name, port, tags, and applicable typed fields. Null properties SHALL not produce empty labels; valid false, zero, empty string, and empty details values SHALL remain understandable. For bulk search, each matched line SHALL keep its own normalized summary and source-attributed groups, and the overall response summary SHALL expose the number of matched, not-found, and invalid lines.

#### Scenario: Multiple sources remain separate
- **WHEN** E09 returns two providers and two port variants
- **THEN** the page renders one group summary with each source as a separate labelled source entry

#### Scenario: Bulk matches remain grouped by input
- **WHEN** two submitted lines each match one or more IOC groups
- **THEN** the page renders separate result sections in submission order rather than merging unrelated input values

#### Scenario: Invalid and absent lines have no false result card
- **WHEN** a bulk item is invalid or not found
- **THEN** the page renders its status and explanation without presenting an IOC group as if it matched
