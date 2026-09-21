## Purpose

Gives administrators provider capability and write-only credential management without exposing secret values.

## ADDED Requirements

### Requirement: Provider API-key policy

Provider configuration SHALL persist and expose `requiresApiKey`. Seeded ThreatFox SHALL default to `true`; seeded PhishTank SHALL default to `false`. When `requiresApiKey=true` and the credential is missing or undecryptable, only that provider fetch SHALL fail with a non-sensitive configuration error and a failed job-history entry. When `requiresApiKey=false` and no credential exists, the provider SHALL be invoked using its supported anonymous mode.

#### Scenario: Required and optional provider policies are enforced

- **WHEN** a provider fetch starts without a configured credential
- **THEN** a required provider fails with a non-sensitive terminal configuration error while an optional provider uses anonymous mode

### Requirement: Write-only provider credential management

The admin API SHALL provide Admin-authorized set/replace, clear, and non-sensitive status operations for supported provider credentials. Values SHALL be accepted only in a protected request body and SHALL never be returned, masked into ordinary DTOs, logged, cached, or included in a URL. Status SHALL contain only configured state, update time, and key version.

#### Scenario: Administrator rotates a provider credential

- **WHEN** an Admin submits a valid replacement credential
- **THEN** the encrypted credential is atomically persisted and the response contains only non-sensitive metadata

#### Scenario: Non-admin attempts credential management

- **WHEN** an authenticated non-admin caller mutates or inspects credential status
- **THEN** the API returns 403 and makes no credential change

### Requirement: Safe credential validation and clearing

Credential operations SHALL reject blank, oversized, unsupported, or invalid submissions with 400, preserve the previous credential on validation/encryption failure, and clear a credential only through an explicit authenticated Admin action.

#### Scenario: Invalid replacement preserves the previous value

- **WHEN** an administrator submits a blank, oversized, or unsupported credential update
- **THEN** the API returns 400 and the previously stored credential remains unchanged
