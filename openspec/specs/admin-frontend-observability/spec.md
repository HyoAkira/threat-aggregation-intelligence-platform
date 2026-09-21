# Admin Frontend Observability Specification

## Purpose

Gives administrators an accurate, bounded view of grouped database counts and structured application logs through safe frontend presentation.

## Requirements

### Requirement: Exact database summary
`/admin/summary` SHALL display the E10 `total`, `ipv4`, `ipv6`, `domain`, and `url` values without client-side recomputation. It SHALL show the time of the latest successful retrieval and provide explicit loading, refresh, stale, and failure states.

#### Scenario: Summary refresh succeeds
- **WHEN** a refreshed response contains changed counts
- **THEN** the displayed values and successful-retrieval time update from that response

#### Scenario: Summary refresh fails
- **WHEN** refresh fails after a summary was loaded
- **THEN** the prior values remain visible and are clearly marked stale with a retry action

### Requirement: Bounded log controls
`/admin/logs` SHALL provide an optional supported-level filter and an integer count control defaulting to 100 and constrained to 1 through 500. Invalid values SHALL produce field-level feedback and SHALL NOT issue a request. The frontend SHALL NOT accept or construct a log file path.

#### Scenario: Count exceeds the maximum
- **WHEN** an Admin enters a count of 501
- **THEN** validation explains the maximum and no log request is sent

### Requirement: Safe structured log presentation
The log viewer SHALL display newest-first timestamp, level, message, and optional exception fields returned by E10. It SHALL support explicit refresh and distinguish loading, empty, stale, and failure states. Every returned string SHALL be rendered as plain text.

#### Scenario: Arbitrary log content is returned
- **WHEN** a message or exception contains HTML-like content
- **THEN** the content appears literally and cannot execute or create markup

#### Scenario: Log refresh fails
- **WHEN** refreshing previously loaded logs fails
- **THEN** prior entries remain visible with a stale indicator and retry control

### Requirement: Admin Summary timeline controls

`/admin/summary` SHALL retain the existing grouped IOC metric cards and provide a timeline control with `7 days` selected by default, a `1 month` option, and a custom start/end date option. Presets and custom dates SHALL be calculated as local calendar boundaries in the detected browser timezone, then sent as a bounded UTC instant range together with that timezone identifier. A custom range SHALL require both dates, reject an inverted or overlong local range with field-level feedback, and SHALL not issue a timeline request while invalid.

#### Scenario: Admin selects a preset
- **WHEN** an Admin changes the timeline from 7 days to 1 month
- **THEN** the selected control changes and the chart requests only the corresponding bounded range derived from the user's local calendar

#### Scenario: Admin enters an invalid custom range
- **WHEN** the custom end date is before the start date or the range exceeds the supported maximum
- **THEN** the UI shows validation feedback and does not send a timeline request

### Requirement: Added and updated activity chart

The Summary page SHALL render a line graph with distinct `Added` and `Updated` series from the validated timeline response. The x-axis and tooltips SHALL present chronological dates and times in the user's detected timezone, missing local dates SHALL remain visible as zero-valued points, and the chart SHALL expose its series names, selected local range, timezone, and point values through accessible labels or an equivalent text representation. When the response reports no successful fetches, the page SHALL show an explicit no-activity state while retaining the selected range and existing metric cards.

#### Scenario: Timeline contains activity
- **WHEN** the selected range contains successful fetches with added or updated counters
- **THEN** the chart shows separate Added and Updated lines with local dates in ascending order

#### Scenario: Timeline is empty
- **WHEN** the selected range contains no successful fetches
- **THEN** the page shows a no-activity message and does not display stale values from a different range as current

### Requirement: Local timezone disclosure and fallback

The Summary page SHALL visibly identify the timezone used for timeline values and SHALL show the current browser offset in `UTC±HH:MM` form, including `UTC+00:00` for UTC and minute-level offsets such as `UTC+05:45`. The displayed local date controls, chart labels, and tooltips SHALL use the same timezone. If the browser does not provide a valid timezone identifier, the page SHALL explicitly fall back to UTC and disclose `UTC+00:00` rather than silently using an unknown timezone.

#### Scenario: User is three hours ahead of UTC
- **WHEN** the browser reports a current offset of three hours ahead
- **THEN** the Summary page identifies the timeline as local time and displays `UTC+03:00`

#### Scenario: User has a fractional offset
- **WHEN** the browser reports a timezone offset of five hours and forty-five minutes ahead of UTC
- **THEN** the Summary page displays `UTC+05:45` and uses that timezone for chart date labels and values

#### Scenario: Browser timezone changes
- **WHEN** the browser timezone changes while the Summary page is open
- **THEN** the displayed offset and timeline query update to the new timezone without retaining points from the prior timezone as current

#### Scenario: Browser timezone is unavailable
- **WHEN** no valid browser timezone identifier can be obtained
- **THEN** the page explicitly displays `UTC+00:00`, requests UTC timeline data, and labels the chart as UTC

### Requirement: Bounded timeline refresh and continuity

The timeline SHALL support explicit refresh and refresh automatically no more frequently than every 30 seconds while the page is visible. Automatic polling SHALL pause while the document is hidden. When a refresh fails after timeline data was loaded, the prior successful points SHALL remain visible with a stale indicator and retry action. When a provider fetch reaches a terminal state, the selected local range and timezone query SHALL be invalidated or refreshed without changing the Admin's selected range or timezone disclosure.

#### Scenario: Timeline refresh fails after data was loaded
- **WHEN** a refresh request fails after a successful timeline response
- **THEN** the previous points remain visible, the chart is marked stale, and a retry action is available

#### Scenario: Page becomes hidden
- **WHEN** the Admin Summary document is hidden
- **THEN** automatic timeline polling stops until the document becomes visible again

#### Scenario: Provider fetch completes
- **WHEN** a provider job reaches a terminal state while the Summary page is open
- **THEN** the current selected range is refreshed once and the range controls are unchanged