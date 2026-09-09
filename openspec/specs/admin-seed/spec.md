## Purpose

Creates the first administrator account from configured credentials on first startup only, without ever persisting or logging the plaintext password, so a freshly deployed system has a deterministic, known admin.

## Requirements

### Requirement: First admin seeded from configuration
When no admin user exists, valid admin credentials are configured, and a password-hashing service is available, the system SHALL create a single admin user with the configured username, a securely hashed password, the Admin role, and a UTC creation timestamp. Persistence SHALL consume the hashing service through an abstraction; the production password KDF and startup activation belong to E04.

#### Scenario: First run creates the admin from configuration
- **WHEN** seeding runs on a database with no users and valid configured admin credentials
- **THEN** exactly one admin user exists with the configured username, a hashed password, role Admin, and a UTC `createdAt`

#### Scenario: Plaintext password is never persisted or logged
- **WHEN** the admin user is seeded
- **THEN** no database row or log entry contains the plaintext password, only its hash

### Requirement: Admin seeding is idempotent and non-destructive
Re-running seeding SHALL NOT create a duplicate admin, reset credentials, or change an existing user.

#### Scenario: Restart does not duplicate the admin
- **WHEN** seeding runs again on a database that already has an admin
- **THEN** no additional user is created

#### Scenario: Restart does not reset credentials
- **WHEN** seeding runs again after the admin's password has been changed
- **THEN** the stored hash is left unchanged

### Requirement: Deterministic handling of missing, invalid, and conflicting inputs
The system SHALL fail without modifying users when admin credentials are missing or invalid, when no password-hashing service is available, or when a non-Admin already owns the configured username. Concurrent attempts SHALL converge on one configured Admin or report a clear conflict.

#### Scenario: Missing or invalid admin settings fail fast
- **WHEN** configured admin credentials are missing or invalid at startup
- **THEN** the startup seed fails fast with a clear error and no user is created

#### Scenario: Existing same-name non-Admin is a startup conflict
- **WHEN** a non-admin user with the configured username already exists
- **THEN** seeding fails with a clear configuration-conflict error and does not alter that user's password or role

#### Scenario: Password hasher is unavailable
- **WHEN** first-admin seeding is invoked without a registered password-hashing service
- **THEN** startup fails clearly and no user is created

#### Scenario: Concurrent startup produces a single admin
- **WHEN** two processes seed concurrently against the same database
- **THEN** exactly one configured admin row exists; a losing insert re-reads that row and completes successfully only when it matches the configured username and Admin role