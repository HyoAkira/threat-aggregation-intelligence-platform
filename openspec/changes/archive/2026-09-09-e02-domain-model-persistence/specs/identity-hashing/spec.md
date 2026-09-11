## Purpose

Provides a deterministic SHA-256 hash of a normalized IOC value, persisted as exactly 32 bytes, so provider-scoped identity can be indexed safely while still verifying the original value on lookup.

## ADDED Requirements

### Requirement: Hash is SHA-256 over UTF-8 bytes of the normalized value
The service SHALL compute the hash from the UTF-8 byte representation of the normalized IOC value and SHALL return exactly 32 bytes.

#### Scenario: Fixed test vectors match
- **WHEN** a known normalized value is hashed
- **THEN** the result equals the fixed SHA-256 vector computed over the UTF-8 bytes of that value

#### Scenario: Output is exactly 32 bytes
- **WHEN** any normalized value is hashed
- **THEN** the returned hash is exactly 32 bytes

### Requirement: Hashing is deterministic
Hashing the same normalized value SHALL always produce the same hash.

#### Scenario: Identical values hash identically
- **WHEN** the same normalized value is hashed twice
- **THEN** both hashes are byte-identical

### Requirement: Hash lookup verifies the original value
After resolving a row by hash, the system SHALL compare the stored normalized value to the incoming value to confirm equality, so a hash collision cannot be silently treated as a match.

#### Scenario: Hash match is confirmed by value comparison
- **WHEN** a row is found by hash lookup for an incoming value
- **THEN** the system compares the stored value with the incoming value and treats the row as matching only when the values are equal

#### Scenario: Equal-value round-trip verifies the original value
- **WHEN** a stored value is reloaded alongside its hash
- **THEN** re-hashing the reloaded value reproduces the stored hash and the original value can be recovered from the lookup