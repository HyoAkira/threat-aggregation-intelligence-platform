## Purpose

Allow administrators to select and operate a dedicated, potentially lower-cost chat model without coupling its credentials or availability to the existing IOC AI report model.

## ADDED Requirements

### Requirement: Separate Admin chat connection settings

Admin-only chat configuration SHALL include a Base URL, model ID, and enabled state distinct from AI report settings. Its API key SHALL use a separate write-only credential identity; reads SHALL expose only credential status, never the plaintext key. Changes to chat configuration SHALL not change report configuration or provider runtime settings, and vice versa. Non-Admin callers SHALL be denied access to configuration and credential operations.

#### Scenario: Admin selects a chat model
- **WHEN** an Admin saves a valid chat Base URL and model ID and configures the chat API key
- **THEN** chat uses that configuration while AI report generation keeps its own settings and credential

#### Scenario: Settings are read
- **WHEN** an Admin opens the chat settings area
- **THEN** it receives non-secret values and API-key configured status, without the key itself

#### Scenario: Non-Admin tries to change chat settings
- **WHEN** a User or anonymous caller requests a chat setting or credential mutation
- **THEN** the request is denied

### Requirement: Fail-closed chat availability

The system SHALL reject an enabled configuration lacking a usable absolute HTTP or HTTPS Base URL or non-empty model ID. Chat SHALL remain unavailable when disabled, when a required credential is unavailable, or when the configured model endpoint cannot perform tool calling; these failures SHALL return safe, stable errors without attempting an answer from ungrounded model knowledge. Public IOC browsing and AI report generation SHALL continue independently.

#### Scenario: Chat model lacks tool calling
- **WHEN** the configured model cannot issue the required tool calls
- **THEN** the chat request fails safely instead of answering from unsupported knowledge

#### Scenario: Chat is disabled
- **WHEN** chat is disabled by an Admin
- **THEN** public IOC pages remain available and chat requests fail with a safe disabled response

