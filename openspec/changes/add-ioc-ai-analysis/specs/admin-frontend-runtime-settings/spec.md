## MODIFIED Requirements

### Requirement: Non-secret settings form

`/admin/settings` SHALL render the supported non-secret global log level, provider enabled/interval/capability fields, and a separate AI configuration area for Base URL, model ID, enabled state, published report instruction text/version, and write-only API credential controls. It SHALL never request or display environment values, plaintext credentials, connection strings, JWT settings, admin passwords, or file paths. The AI settings and report instructions SHALL use their own Admin endpoints and SHALL not be included in the existing complete provider/log settings PUT snapshot.

#### Scenario: Settings load succeeds
- **WHEN** an Admin opens the settings route
- **THEN** the form shows database-backed non-secret runtime and AI settings, report instruction controls, and credential status without plaintext values

#### Scenario: Full provider settings save
- **WHEN** the Admin saves the complete provider and log-level snapshot
- **THEN** the request contains no AI API key, AI connection settings, or report instruction text and those separate values are unchanged

## ADDED Requirements

### Requirement: AI configuration and instruction editing feedback

The Admin AI area SHALL offer accessible validation, loading, success, conflict, and failure states for connection and instruction saves. A failed save SHALL preserve the draft. The instruction editor SHALL show the current version and support reviewing earlier versions. The credential input SHALL be cleared after set/replace completes and SHALL never enter browser query caches or URLs.

#### Scenario: Report instruction publish conflicts
- **WHEN** another Admin has published a newer instruction version before the current draft is saved
- **THEN** the UI shows a conflict and retains the unsaved draft for review
