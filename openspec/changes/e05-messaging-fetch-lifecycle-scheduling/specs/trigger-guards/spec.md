## Purpose

Enforces provider capability and cadence rules before a fetch is enqueued or executed, so cooldowns and enabled/running state are checked both at trigger time and at consume time. Provider scheduling uses the configured interval; PhishTank's seeded interval is 8 hours.

## ADDED Requirements

### Requirement: Provider capability validation
The system SHALL know which modes each provider supports (ThreatFox: recent and full; PhishTank: full only) and SHALL reject a requested mode the provider does not expose.

#### Scenario: Supported mode accepted
- **WHEN** a trigger requests ThreatFox recent or full, or PhishTank full
- **THEN** the mode is accepted for capability checking

#### Scenario: Unsupported mode rejected
- **WHEN** a trigger requests PhishTank recent or an unknown mode
- **THEN** the request is rejected with 400 without publishing

### Requirement: Cooldown enforcement
The system SHALL reject a fetch whose provider/mode is on cooldown: ThreatFox recent fetches SHALL be at least 5 minutes apart and ThreatFox full fetches at least 60 minutes apart, measured from the latest matching `fetchHistory.startedAt`. A started attempt SHALL consume the cooldown even if it later fails.

#### Scenario: Recent fetch within cooldown rejected
- **WHEN** a ThreatFox recent fetch is requested less than 5 minutes after the latest recent attempt started
- **THEN** the trigger is rejected with 409 and nothing is published

#### Scenario: Full fetch within cooldown rejected
- **WHEN** a ThreatFox full fetch is requested less than 60 minutes after the latest full attempt started
- **THEN** the trigger is rejected with 409 and nothing is published

#### Scenario: Cooldown expired accepted
- **WHEN** a ThreatFox fetch is requested after its cooldown has elapsed
- **THEN** the trigger is accepted and a job is published

### Requirement: Enabled and running-state guards
The system SHALL reject a fetch for a disabled provider and SHALL reject a fetch while a previous job for the same provider is still queued or running.

#### Scenario: Disabled provider rejected
- **WHEN** a trigger or scheduled tick targets a disabled provider
- **THEN** the trigger is rejected (409 for manual) and nothing is published

#### Scenario: Provider already running rejected
- **WHEN** a trigger targets a provider with a Queued or Running history row
- **THEN** the trigger is rejected with 409 and nothing is published

### Requirement: Guards are atomic and rechecked at consume
The system SHALL reserve an allowed logical fetch atomically so simultaneous trigger requests and scheduler ticks cannot both pass the same guard, and the worker consumer SHALL re-evaluate persisted enabled/running/cooldown state before executing a delivered message.

#### Scenario: No double reservation
- **WHEN** two guard evaluations for the same provider/mode run concurrently
- **THEN** only one succeeds and the other receives the appropriate rejection

#### Scenario: Obsolete duplicate message skipped
- **WHEN** a stale or duplicate FetchJob is delivered and the persisted guards no longer allow it
- **THEN** the consumer skips execution without starting another fetch
