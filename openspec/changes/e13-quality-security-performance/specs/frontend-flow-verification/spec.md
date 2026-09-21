## Purpose

Verifies critical User and Admin browser workflows, accessibility states, and hostile-text handling through deterministic frontend boundaries suitable for CI.

## ADDED Requirements

### Requirement: Deterministic frontend API boundary
Frontend component and flow tests SHALL use validated fixture contracts through an injected client or request-interception boundary and SHALL NOT require live providers, SQL Server, or RabbitMQ. The harness SHALL support loading, success, empty, 400, 401, 403, 404, 409, unexpected failure, and delayed response states.

#### Scenario: Same fixture is repeated
- **WHEN** a flow test runs repeatedly with the same API fixture
- **THEN** it renders the same state and sends the same contract requests in CI

### Requirement: Critical User and Admin flows
Automated browser-capable tests SHALL cover login, deep-link resume, expiry/logout/cache clearing, role routing, search validation/not-found/grouped success, browse URL filters and history, detail source attribution and relations, Admin provider/manual-trigger/history/summary/log/settings flows, failed-save preservation, and duplicate-action prevention.

#### Scenario: User attempts an Admin workflow
- **WHEN** a User session navigates to an Admin route
- **THEN** Admin data is not requested or rendered and the documented forbidden behavior occurs

### Requirement: Accessibility and unsafe-text regression coverage
Critical flows SHALL include keyboard, focus, accessible-name, dialog, status-announcement, and basic automated accessibility checks. Provider, IOC, log, and error strings containing markup SHALL remain inert text.

#### Scenario: Hostile external text is displayed
- **WHEN** a fixture contains HTML or script-like text in an external field
- **THEN** no executable markup is created and the literal value remains inspectable
