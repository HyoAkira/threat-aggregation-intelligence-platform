## MODIFIED Requirements

### Requirement: Non-secret settings form

`/admin/settings` SHALL render the supported non-secret global log level and provider enabled/interval/capability fields under a Runtime section, a separate AI analysis area for report connection settings and published report instructions, and a distinct AI chat area for chat Base URL, model ID, enabled state, and write-only credential controls. It SHALL never request or display environment values, plaintext credentials, connection strings, JWT settings, admin passwords, or file paths. AI analysis and chat settings SHALL use their own Admin endpoints and SHALL not be included in the existing complete provider/log settings PUT snapshot.

#### Scenario: Settings load succeeds

- **WHEN** an Admin opens the settings route
- **THEN** the form shows database-backed non-secret runtime, report, and chat settings plus credential status without plaintext values

#### Scenario: Full provider settings save

- **WHEN** the Admin saves the complete provider and log-level snapshot
- **THEN** the request contains no AI credentials, connection settings, or report instruction text and those separate values are unchanged

#### Scenario: Chat settings save

- **WHEN** an Admin changes the chat model settings
- **THEN** the chat settings are saved independently of the AI report and provider settings

## ADDED Requirements

### Requirement: Chat settings editing feedback

The Admin chat area SHALL provide accessible validation, loading, success, and failure states for the chat connection and credential operations. A failed save SHALL preserve the draft. The credential input SHALL clear after a successful set or replacement and SHALL never enter browser query caches or URLs. The area SHALL state that chat requires model tool-call support.

#### Scenario: Chat settings save fails

- **WHEN** the Admin submits invalid or unavailable chat settings
- **THEN** the UI shows the failure and preserves the unsaved non-secret draft for correction

#### Scenario: Chat key is replaced

- **WHEN** the Admin successfully replaces the chat API key
- **THEN** the UI shows configured status and clears the plaintext input without displaying the stored value

