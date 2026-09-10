# batch-transaction Specification

## Purpose

Defines the whole-batch IOC transaction for a single fetch: records are processed in bounded chunks within one outer transaction, any invalid record rolls back all IOC and tag changes, and fetch history is written outside the transaction.

## Requirements

### Requirement: Records are processed in bounded chunks
The pipeline SHALL process approximately 2,000 provider records at a time, saving each chunk and clearing EF change tracking while retaining one outer transaction, so tracked entity count stays bounded regardless of feed size.

#### Scenario: Tracked entity count stays bounded
- **WHEN** a fetch contains more records than one chunk
- **THEN** the pipeline saves and clears tracking after each chunk and the tracked entity count never grows unbounded

### Requirement: The whole batch is one transaction
All IOC and tag changes across every chunk SHALL participate in a single outer transaction that commits atomically or rolls back entirely.

#### Scenario: A valid batch commits once
- **WHEN** every record in the fetch is valid
- **THEN** all IOC and tag changes commit together

#### Scenario: An invalid record rolls back all changes
- **WHEN** any record in the fetch is invalid, including a final record
- **THEN** all IOC and tag changes from every chunk roll back and no partial data is written

### Requirement: Tags are not duplicated across chunks
Because every chunk shares the outer transaction and the tag cache, the pipeline SHALL ensure a tag created in an earlier chunk is not recreated or duplicated when a later chunk fails.

#### Scenario: Rollback removes tags from earlier chunks
- **WHEN** a later chunk fails after earlier chunks inserted tags
- **THEN** those earlier tag insertions are rolled back with the rest of the batch

### Requirement: History is written outside the IOC transaction
Fetch-history SHALL be persisted independently of the IOC transaction so a rolled-back batch still records its outcome.

#### Scenario: Failed batch records failed history
- **WHEN** the IOC batch rolls back
- **THEN** the separate history row records Failed status with zero added and zero updated counts and names the offending record

#### Scenario: Successful batch records committed counters
- **WHEN** the IOC batch commits
- **THEN** the separate history row records the committed added and updated counts