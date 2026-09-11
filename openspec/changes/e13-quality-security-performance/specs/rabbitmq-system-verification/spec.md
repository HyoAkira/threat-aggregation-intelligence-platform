## Purpose

Proves message topology and worker lifecycle behavior against an isolated real RabbitMQ boundary with deterministic timing and identity.

## ADDED Requirements

### Requirement: Isolated real-broker harness
Broker verification SHALL use a real RabbitMQ instance with a unique virtual host or endpoint namespace per run. It SHALL prove durable topology, confirmed publishing, reconnect behavior, one-at-a-time consumption, redelivery idempotency, one logical history per `jobId`, immediate terminal failures, exhausted retry routing, and cleanup isolation.

#### Scenario: Duplicate delivery reuses one logical job
- **WHEN** the same `jobId` is delivered more than once
- **THEN** persisted data and history are applied no more than once and no second logical history is created

#### Scenario: Retries are exhausted
- **WHEN** a retryable job fails every allowed execution
- **THEN** executions follow the configured attempt order and the final message reaches the expected `_error` queue

### Requirement: Production retry contract remains verifiable
The production retry delays SHALL remain 10 seconds, 30 seconds, and 60 seconds with four total executions. Broker tests MAY inject shorter delays for deterministic CI, but a separate configuration test SHALL lock the production values and the real-broker test SHALL still verify attempt count, ordering, and terminal routing.

#### Scenario: CI uses shortened delays
- **WHEN** the integration harness replaces delays with bounded test values
- **THEN** the broker behavior is exercised quickly while a passing contract test independently proves the production schedule
