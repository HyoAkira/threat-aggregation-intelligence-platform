## Purpose

Provides the durable RabbitMQ bus between the API/scheduler publishers and the worker consumer, with publisher confirmation, bounded consumption, and dead-lettering of exhausted jobs.

## ADDED Requirements

### Requirement: Durable topology
The system SHALL declare a durable exchange and a durable work queue with a fixed routing key, and SHALL publish persistent messages so the topology and in-flight jobs survive a broker restart.

#### Scenario: Topology survives broker restart
- **WHEN** the broker is restarted after the API or worker has declared the topology
- **THEN** the exchange, queue, and bindings still exist and publishers and consumers reconnect and operate

#### Scenario: Pending message survives broker restart
- **WHEN** a FetchJob is published but not yet consumed and the broker restarts
- **THEN** the message is still present on the queue after the broker returns

### Requirement: Publisher confirmation
The system SHALL publish FetchJob messages with publisher confirmation so the caller knows a message reached the broker before reporting success.

#### Scenario: Confirmed publish
- **WHEN** a publisher publishes a FetchJob and the broker acknowledges it
- **THEN** the publish is confirmed and the caller can return 202 Accepted

#### Scenario: Failed publish is terminal for the job
- **WHEN** a publisher publishes a FetchJob and the broker does not confirm it (connection lost or broker rejects it)
- **THEN** the related history row is marked Failed and no 202 is returned

### Requirement: Bounded consumption
The system SHALL consume FetchJob messages with a prefetch count of one and a consumer concurrency of one so only one fetch executes at a time globally.

#### Scenario: One message in flight
- **WHEN** multiple FetchJobs are queued
- **THEN** only one is delivered for execution at a time and the rest wait on the queue

#### Scenario: Consumer preserves order of delivery
- **WHEN** the consumer is processing a message
- **THEN** no further message is delivered until the current one is acknowledged or faulted

### Requirement: Default error queue
A message whose retries are exhausted SHALL move to the default `_error` queue rather than being redelivered indefinitely.

#### Scenario: Exhausted message dead-lettered
- **WHEN** a fetch fails transiently more times than the configured retry count
- **THEN** the message is routed to the default `_error` queue and the history row is marked Failed

#### Scenario: Error message is inspectable
- **WHEN** a message lands in the `_error` queue
- **THEN** it retains the original FetchJob payload and the failure reason so an operator can inspect and replay it