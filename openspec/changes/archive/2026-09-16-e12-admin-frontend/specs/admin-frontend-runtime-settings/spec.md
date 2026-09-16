## Purpose

Lets administrators safely edit the supported non-secret runtime settings as one validated atomic change while protecting unsaved work.

## ADDED Requirements

### Requirement: Non-secret settings form
`/admin/settings` SHALL render only the E10 global log level and provider enabled/interval fields. It SHALL never request or display environment values, credentials, provider keys, connection strings, JWT settings, admin passwords, or file paths.

#### Scenario: Settings load succeeds
- **WHEN** an Admin opens the settings route
- **THEN** the form contains only database-backed runtime settings and provider controls

### Requirement: Client validation mirrors server constraints
The complete draft SHALL be validated before saving. Log level SHALL be one of `Verbose`, `Debug`, `Information`, `Warning`, `Error`, or `Fatal`; each interval SHALL be an integer at least the API-supplied `minimumIntervalMinutes`. Server validation SHALL remain authoritative.

#### Scenario: One interval is below its minimum
- **WHEN** any provider interval is invalid
- **THEN** that field receives accessible feedback, no PUT is sent, and every draft value is preserved

### Requirement: Atomic settings save
One Save action SHALL submit the complete editable snapshot through E10's atomic PUT. Duplicate submission SHALL be disabled while saving. Success SHALL replace the form baseline with the returned snapshot, clear dirty state, refresh provider/settings data, and state that runtime behavior reflects changes within 30 seconds. Failure SHALL preserve all draft values and map field errors when supplied.

#### Scenario: Save fails
- **WHEN** the PUT returns ProblemDetails
- **THEN** all unsaved values remain, the error is visible, and no success state is shown

#### Scenario: Save succeeds
- **WHEN** the PUT returns the persisted non-secret settings snapshot
- **THEN** that response becomes the clean baseline and the application-time message is shown

### Requirement: Unsaved-change protection
Dirty state SHALL be calculated against the latest successful server snapshot. Application navigation, reload, tab close, and logout SHALL warn before discarding dirty settings. Choosing to stay SHALL preserve the draft; choosing to leave SHALL discard it. Reset SHALL restore the latest successful snapshot.

#### Scenario: Dirty navigation is cancelled
- **WHEN** an Admin edits a value, attempts to leave, and chooses to stay
- **THEN** navigation is cancelled and the edit remains
