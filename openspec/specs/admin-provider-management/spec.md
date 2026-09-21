# Admin Provider Management Specification

## Purpose

Gives administrators a data-driven way to register and maintain provider integrations and their fetch capabilities without code changes, and exposes the persisted capabilities to the admin provider dashboard.

## Requirements

### Requirement: Advertised provider capabilities
The admin provider dashboard SHALL derive each provider's `supportedModes`, `defaultMode`, per-mode cooldowns, and `minimumIntervalMinutes` from the persisted provider configuration rather than from built-in provider-name rules. The dashboard SHALL expose every field the create/update endpoints accept, using the same field names, so a listed provider can be edited and submitted back without field translation.

#### Scenario: Modes come from stored configuration
- **WHEN** an Admin lists providers
- **THEN** each provider reports `supportedModes`, `defaultMode`, `recentCooldownMinutes`, `fullCooldownMinutes`, and `minimumIntervalMinutes` matching its stored configuration

#### Scenario: Capability change is reflected
- **WHEN** an admin updates a provider's supported modes and then lists providers
- **THEN** the dashboard advertises the updated mode set

### Requirement: Create provider
`POST /api/admin/providers` SHALL require the Admin role and create a provider from `{name, baseUrl, intervalMinutes, isEnabled, defaultMode, supportedModes, recentCooldownMinutes, fullCooldownMinutes, minimumIntervalMinutes}`. It SHALL return **201** with the created provider, and SHALL reject invalid input with **400** and no persisted row.

#### Scenario: Valid provider is created
- **WHEN** an Admin submits a provider whose modes, intervals, and cooldowns are valid
- **THEN** the provider row is persisted with its configuration and 201 with the created provider is returned

#### Scenario: Invalid provider is rejected
- **WHEN** an Admin submits a provider with a blank name, an invalid URL, a non-positive interval, an empty or undefined mode set, a default mode outside the supported set, or a non-positive cooldown
- **THEN** the API returns 400 and no provider row is created

#### Scenario: Duplicate provider name is rejected
- **WHEN** an Admin submits a provider whose name already exists
- **THEN** the API returns 400 and no provider row is created

### Requirement: Update provider
`PUT /api/admin/providers/{id}` SHALL require the Admin role and replace a provider's configuration with the submitted values. It SHALL return **404** for an unknown provider, **400** for invalid input, and a success response with the updated provider otherwise.

#### Scenario: Valid update is persisted
- **WHEN** an Admin submits a valid update for an existing provider
- **THEN** the stored configuration is replaced and the updated provider is returned

#### Scenario: Listed provider payload round-trips
- **WHEN** an Admin builds an update request from a listed provider's fields
- **THEN** the update is accepted without field translation and the stored configuration is replaced

#### Scenario: Unknown provider update returns 404
- **WHEN** an Admin submits an update for a provider that does not exist
- **THEN** the API returns 404 and nothing is persisted

### Requirement: Delete provider
`DELETE /api/admin/providers/{id}` SHALL require the Admin role and delete a provider that has no fetch history and no ingested IOC records. It SHALL return **404** for an unknown provider and **409** when the provider is still referenced by fetch history or IOC records, so ingested data is never destroyed implicitly.

#### Scenario: Unreferenced provider is deleted
- **WHEN** an Admin deletes a provider with no fetch history and no IOC records
- **THEN** the provider row is removed and a success response is returned

#### Scenario: Referenced provider deletion is refused
- **WHEN** an Admin deletes a provider that has fetch history or IOC records
- **THEN** the API returns 409 and the provider and its data remain

### Requirement: Provider management authorization
Every provider management write endpoint SHALL require the Admin role and SHALL reject authenticated non-admin callers with **403**.

#### Scenario: Non-admin is forbidden
- **WHEN** an authenticated User calls a provider create, update, or delete endpoint
- **THEN** the API returns 403 ProblemDetails and no change is persisted
