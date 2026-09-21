## MODIFIED Requirements

### Requirement: Provider entity invariants
A `Provider` SHALL require a non-empty display/integration name and a non-empty base URL, an interval of at least one minute, a defined default mode that is included in its supported modes, at least one supported mode, cooldown values that are null or positive, and a minimum interval of at least one minute. It SHALL expose fetch-status summary, last-fetch, and next-fetch times as nullable values.

#### Scenario: Provider with blank name or base URL is rejected
- **WHEN** a Provider is created with an empty name or base URL
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with a non-positive interval is rejected
- **WHEN** a Provider is created with an interval less than one minute
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with a default mode outside its supported modes is rejected
- **WHEN** a Provider is created with a default mode that is not in its supported modes
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with no supported mode is rejected
- **WHEN** a Provider is created with an empty supported-mode set
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with a non-positive cooldown is rejected
- **WHEN** a Provider is created with a zero or negative cooldown
- **THEN** construction fails and no invalid Provider exists

#### Scenario: Provider with a non-positive minimum interval is rejected
- **WHEN** a Provider is created with a minimum interval less than one minute
- **THEN** construction fails and no invalid Provider exists
