## MODIFIED Requirements

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
