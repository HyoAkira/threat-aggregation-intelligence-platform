# redelivery-idempotency Specification

## Purpose

Guarantees that redelivered or concurrently delivered messages for the same logical job produce the same final dataset with no duplicate rows and no false added/updated counts.

## Requirements

### Requirement: Unique-key races resolve safely
The pipeline SHALL handle concurrent insert attempts against the same unique identity so that exactly one row wins and the other does not create a duplicate or report a false success.

#### Scenario: Concurrent insert of the same identity produces one row
- **WHEN** two deliveries try to insert the same provider endpoint or provider record identity
- **THEN** only one row is committed and the loser neither duplicates the row nor overstates the added count

### Requirement: Replay of the same job is idempotent
Re-delivering a message for a job that already committed SHALL reproduce the same final dataset as a single delivery, with no duplicate rows and no repeated addition of records.

#### Scenario: Redelivery does not duplicate data
- **WHEN** a committed job's message is delivered again
- **THEN** the final dataset is unchanged from the single-delivery outcome

#### Scenario: Redelivery does not double-count adds
- **WHEN** a committed job's message is delivered again
- **THEN** the added and updated counts reflect a single delivery, not repeated additions

### Requirement: No false success counts on conflict
When a delivery loses a unique-key race or is otherwise rejected, the pipeline SHALL not report it as an added or updated record.

#### Scenario: Losing delivery reports no added record
- **WHEN** a delivery loses a unique-key race
- **THEN** it contributes no added or updated count