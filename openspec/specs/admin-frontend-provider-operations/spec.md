# Admin Frontend Provider Operations Specification

## Purpose

Provides administrators with safe, current provider controls and fetch history without duplicating server-side scheduling or guard decisions.

## Requirements

### Requirement: Admin provider dashboard
`/admin/providers` SHALL require an Admin session and display every provider's name, enabled state, interval, latest status, last completion time, next scheduled time, default mode, supported modes, and authoritative trigger state. Null status and time values SHALL be presented as never run or not scheduled rather than formatted as dates.

#### Scenario: Never-run provider
- **WHEN** a provider has null latest status, last-fetch, and next-fetch values
- **THEN** the dashboard identifies it as never run and explains that no next run is currently scheduled

#### Scenario: User is denied the Admin route
- **WHEN** a signed-in User opens `/admin/providers`
- **THEN** Admin data is not requested or rendered and the E11 forbidden-route behavior is used

### Requirement: Bounded provider refresh
The visible dashboard SHALL poll provider state no more frequently than every 15 seconds and SHALL offer manual refresh. While an accepted logical job is non-terminal, the frontend SHALL poll that `jobId` no more frequently than every 2 seconds, stop after a terminal state, and refresh the affected provider, history, and summary data. Periodic polling SHALL pause while the page is hidden.

#### Scenario: Running job reaches a terminal state
- **WHEN** a polled job changes from Queued or Running to Succeeded or Failed
- **THEN** job polling stops and affected provider, history, and summary queries are refreshed once

#### Scenario: Background refresh fails
- **WHEN** a refresh fails after provider data was previously loaded
- **THEN** the last successful data remains visible with a stale indicator, last-success time, and retry action

### Requirement: Server-authoritative trigger availability
For each supported mode, the dashboard SHALL use API-supplied `canTrigger`, `blockedReason`, and `availableAt` values. Cooldown presentation SHALL include an accessible remaining-time display and absolute UTC availability time. Reaching zero SHALL refresh provider state and SHALL NOT independently override the API's decision.

#### Scenario: Mode is cooling down
- **WHEN** a mode is unavailable with `blockedReason=cooldown` and a future `availableAt`
- **THEN** its action is disabled and both remaining and absolute availability are displayed

### Requirement: Safe manual fetch submission
The page SHALL show only API-reported modes, call the default fetch endpoint for the provider's default mode, and call the full-fetch endpoint for a supported non-default Full mode. Every Full fetch SHALL require accessible confirmation; other modes SHALL not. Only one request per provider SHALL be submitted while a mutation is pending.

#### Scenario: Duplicate activation
- **WHEN** an Admin activates the same provider action twice before the first request settles
- **THEN** only one fetch request is sent

#### Scenario: Full fetch is cancelled
- **WHEN** an Admin cancels a Full-fetch confirmation
- **THEN** no request is sent and focus returns to the trigger control

### Requirement: Fetch outcome handling
A 202 response SHALL display the returned logical `jobId` and begin bounded job polling. A 409 response SHALL show the safe ProblemDetails guard reason, refresh provider state, and SHALL NOT retain an optimistic queued state. Other failures SHALL use E11's safe error and correlation-ID behavior.

#### Scenario: Guard changes before submission
- **WHEN** an action appeared available but the API returns 409
- **THEN** the guard reason is visible, provider state is refreshed, and the page does not claim a job was queued

### Requirement: Paginated provider history
The provider view SHALL expose history using the E10 envelope with page 1 and page size 20 defaults and an API maximum of 100. Rows SHALL display trigger, mode, status, start and finish times, duration when derivable, added and updated counts, and safe terminal error details. Provider and page selection SHALL survive reload and browser Back/Forward navigation.

#### Scenario: History spans pages
- **WHEN** the selected provider has more than one history page
- **THEN** navigation requests only the selected page and cannot move outside the response bounds

### Requirement: Safe terminal-error inspection
Long terminal errors SHALL be summarized in the row and inspectable through an accessible detail view. All error text SHALL wrap within the viewport, preserve useful whitespace, and be rendered as text rather than HTML or Markdown.

#### Scenario: Error contains hostile markup
- **WHEN** an error contains tags, script text, or a long unbroken value
- **THEN** it is displayed literally without execution or layout overflow