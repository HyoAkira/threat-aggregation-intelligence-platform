# Admin Frontend Runtime Settings Specification

## Purpose

Lets administrators safely edit the supported non-secret runtime settings as one validated atomic change while protecting unsaved work.

## Requirements

### Requirement: Non-secret settings form

`/admin/settings` SHALL render the supported non-secret global log level and provider enabled/interval/capability fields under a Runtime section, and a separate AI analysis section for Base URL, model ID, enabled state, published report instruction text/version, and write-only API credential controls. It SHALL never request or display environment values, plaintext credentials, connection strings, JWT settings, admin passwords, or file paths. The AI settings and report instructions SHALL use their own Admin endpoints and SHALL not be included in the existing complete provider/log settings PUT snapshot.

#### Scenario: Settings load succeeds
- **WHEN** an Admin opens the settings route
- **THEN** the form shows database-backed non-secret runtime and AI settings, report instruction controls, and credential status without plaintext values

#### Scenario: Full provider settings save
- **WHEN** the Admin saves the complete provider and log-level snapshot
- **THEN** the request contains no AI API key, AI connection settings, or report instruction text and those separate values are unchanged

### Requirement: AI configuration and instruction editing feedback

The Admin AI area SHALL offer accessible validation, loading, success, conflict, and failure states for connection and instruction saves. A failed save SHALL preserve the draft. The instruction editor SHALL show the current version and support reviewing earlier versions. The credential input SHALL be cleared after set/replace completes and SHALL never enter browser query caches or URLs.

#### Scenario: Report instruction publish conflicts
- **WHEN** another Admin has published a newer instruction version before the current draft is saved
- **THEN** the UI shows a conflict and retains the unsaved draft for review

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