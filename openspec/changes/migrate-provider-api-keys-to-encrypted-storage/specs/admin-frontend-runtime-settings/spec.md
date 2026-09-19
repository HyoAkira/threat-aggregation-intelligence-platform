## Purpose

Extends `/admin/settings` with a separate write-only provider credential area while preserving the non-secret runtime settings contract.

## MODIFIED Requirements

### Requirement: Non-secret settings form

`/admin/settings` SHALL render the supported non-secret runtime settings and provider capability fields, including `requiresApiKey`, but SHALL never place a plaintext credential in the full settings snapshot, ordinary settings response, URL, browser query cache, or rendered provider DTO.

#### Scenario: Settings load succeeds

- **WHEN** an Admin opens the settings route
- **THEN** the form contains only non-secret settings and provider key policy/status metadata

#### Scenario: Full provider settings save

- **WHEN** the Admin saves the complete runtime settings snapshot
- **THEN** the request contains no plaintext provider credential and credential state is unchanged

## ADDED Requirements

### Requirement: Separate write-only credential controls

The settings route SHALL offer a separate credential section per supported provider with set/replace and explicit clear actions. A submitted value SHALL be sent only to the credential endpoint, SHALL be cleared from local component state after the request completes, and SHALL never be populated from a response or persisted in a query cache.

#### Scenario: Credential is rotated

- **WHEN** an Admin submits a new provider credential
- **THEN** the UI shows configured status and update metadata without displaying or retaining the submitted value
