# browse-table-ui Specification

## Purpose

Provides a scalable and accessible browse-table experience for grouped IOC results while preserving bounded server paging, durable navigation state, source attribution, and the established presentation.

## Requirements

### Requirement: Bounded server-paginated table

The browse table SHALL request and render only the active page of grouped IOC results. Page navigation and page-size changes SHALL request the corresponding `page` and `pageSize` from the existing browse endpoint, and the client SHALL NOT fetch the complete matching dataset for browser-side pagination.

#### Scenario: Initial URL loads one page
- **WHEN** the browse page opens with valid filters, ordering, page, and page size in the URL
- **THEN** exactly one browse request is issued for that page and only the returned items are rendered

#### Scenario: Page navigation requests one new page
- **WHEN** the user navigates to another table page
- **THEN** exactly one browse request is issued for the selected page and unrelated pages are not requested

#### Scenario: Page size change starts at page one
- **WHEN** the user selects a supported page size
- **THEN** the URL and table reset to page one, exactly one request is issued with the selected size, and the result summary uses the server-provided matching count

### Requirement: Backend-authoritative ordering and filtering

The browse table SHALL delegate filtering and ordering to the browse endpoint. It SHALL offer ordering only for the backend-supported fields `lastSeenUtc`, `firstSeenUtc`, `iocType`, and `iocValue`, with direction `asc` or `desc`; other columns SHALL NOT be orderable. The table SHALL NOT expose a separate global-search or multi-column-ordering contract.

#### Scenario: Supported column ordering uses the browse contract
- **WHEN** the user orders a supported column
- **THEN** the URL resets to page one and exactly one request is issued with the matching `orderBy` and `dir` values without reordering only the loaded rows in the browser

#### Scenario: Presentation-only columns cannot order
- **WHEN** the table displays providers, threat labels, or row actions that have no browse ordering field
- **THEN** those column headers do not offer ordering and no unsupported ordering value is sent to the endpoint

#### Scenario: Existing filters remain authoritative
- **WHEN** the user applies a type, provider, tag, or observed-period filter
- **THEN** the URL and table reset to page one and the next browse request retains every active filter

### Requirement: Durable single-source navigation state

The browse URL SHALL be the durable source of filter, ordering, page, and page-size state. A table interaction SHALL update that URL without causing a duplicate request, while initial load, reload, Back, Forward, and a shared URL SHALL apply the represented state to the table and issue exactly one request for that view.

#### Scenario: Browser history restores the table
- **WHEN** the user returns to a prior browse URL with Back or Forward
- **THEN** the filters, ordering, page size, active page, and rendered rows are restored with exactly one request

#### Scenario: URL reflection does not duplicate a draw
- **WHEN** a table interaction updates the browse URL
- **THEN** handling that reflected URL state does not trigger a second request for the same normalized view

### Requirement: Current request wins

The browse table SHALL prevent superseded, cancelled, or out-of-order requests from replacing the latest requested view. Cancellation caused by a newer request or component cleanup SHALL NOT be presented as a user-visible failure.

#### Scenario: A late response cannot overwrite newer rows
- **WHEN** an earlier browse request completes after a later request
- **THEN** the table continues to show the later request's state and data

#### Scenario: A superseded request is silent
- **WHEN** a request is cancelled because the user selected another page, filter, page size, or ordering
- **THEN** no error state is shown for the cancelled request

### Requirement: Existing presentation and accessible states are preserved

The table SHALL retain the current IOC columns, detail navigation, copy action, source/provider attribution, result summary, established visual tokens, keyboard-visible focus, accessible names, and responsive horizontal scrolling. Pending, empty, forbidden, and failed requests SHALL remain distinguishable, and an error SHALL NOT be presented as a successful empty result.

#### Scenario: Row actions remain accessible
- **WHEN** a page contains IOC groups
- **THEN** every row provides the existing detail navigation and copy action with accessible names and keyboard-visible focus

#### Scenario: Empty results remain distinct from failure
- **WHEN** the endpoint returns zero matching items
- **THEN** the page shows the existing empty state and does not show an error

#### Scenario: Failed request replaces stale table state
- **WHEN** the current browse request fails
- **THEN** the page shows the corresponding forbidden or retryable error state without labelling stale rows as the current result

#### Scenario: Narrow viewport remains usable
- **WHEN** the table is viewed at a width smaller than its columns require
- **THEN** the result surface scrolls horizontally without causing page-level horizontal overflow
