# Trigger Guards Specification

## Purpose

Enforces provider capability and cadence rules before a fetch is enqueued or executed, so cooldowns and enabled/running state are checked both at trigger time and at consume time. Provider scheduling uses the configured interval; PhishTank's seeded interval is 8 hours.

## Requirements

### Requirement: Provider capability validation
The system SHALL know which modes each provider supports from the persisted provider configuration (`supportedModes`) and SHALL reject a requested mode the provider does not expose. The seeded defaults SHALL be ThreatFox recent and full, and PhishTank full only.

#### Scenario: Supported mode accepted
- **WHEN** a trigger requests a mode the provider's persisted `supportedModes` contains
- **THEN** the mode is accepted for capability checking

#### Scenario: Unsupported mode rejected
- **WHEN** a trigger requests a mode absent from the provider's persisted `supportedModes`
- **THEN** the request is rejected with 400 without publishing

#### Scenario: Updated capability is honored without a code change
- **WHEN** an admin adds or removes a supported mode on a provider and a trigger then requests that mode
- **THEN** the guard accepts or rejects the request according to the updated provider configuration

### Requirement: Cooldown enforcement
The system SHALL reject a fetch whose provider/mode is on cooldown using the provider's persisted per-mode cooldown, measured from the latest matching `fetchHistory.startedAt`. A null cooldown SHALL mean no cooldown for that mode. A started attempt SHALL consume the cooldown even if it later fails. The seeded defaults SHALL be ThreatFox recent at least 5 minutes, ThreatFox full at least 60 minutes, and PhishTank no cooldown.

#### Scenario: Recent fetch within cooldown rejected
- **WHEN** a ThreatFox recent fetch is requested less than the persisted recent cooldown after the latest recent attempt started
- **THEN** the trigger is rejected with 409 and nothing is published

#### Scenario: Full fetch within cooldown rejected
- **WHEN** a ThreatFox full fetch is requested less than the persisted full cooldown after the latest full attempt started
- **THEN** the trigger is rejected with 409 and nothing is published

#### Scenario: Cooldown expired accepted
- **WHEN** a provider/mode is requested after its persisted cooldown has elapsed
- **THEN** the trigger is accepted and a job is published

#### Scenario: Null cooldown permits immediate repeat
- **WHEN** a provider/mode has a null persisted cooldown and no active job
- **THEN** the fetch is not rejected for cooldown reasons

### Requirement: Enabled and running-state guards
The system SHALL reject a fetch for a disabled provider and SHALL reject a fetch while a previous job for the same provider is still queued or running.

#### Scenario: Disabled provider rejected
- **WHEN** a trigger or scheduled tick targets a disabled provider
- **THEN** the trigger is rejected (409 for manual) and nothing is published

#### Scenario: Provider already running rejected
- **WHEN** a trigger targets a provider with a Queued or Running history row
- **THEN** the trigger is rejected with 409 and nothing is published

### Requirement: Guards are atomic and rechecked at consume
The system SHALL reserve an allowed logical fetch atomically so simultaneous trigger requests and scheduler ticks cannot both pass the same guard, and the worker consumer SHALL re-evaluate persisted enabled/running/cooldown state before executing a delivered message. The consume-time check SHALL identify the current reservation by `jobId`: that job's own Queued/Running row SHALL NOT reject itself, while another active row or a terminal/obsolete current row SHALL prevent execution.

#### Scenario: No double reservation
- **WHEN** two guard evaluations for the same provider/mode run concurrently
- **THEN** only one succeeds and the other receives the appropriate rejection

#### Scenario: Obsolete duplicate message skipped
- **WHEN** a stale or duplicate FetchJob is delivered and the persisted guards no longer allow it
- **THEN** the consumer skips execution without starting another fetch

#### Scenario: Current reservation passes its own recheck
- **WHEN** a newly delivered FetchJob has the matching current Queued history row and no other active job
- **THEN** its own reservation does not trigger the queued/running rejection and execution may start
