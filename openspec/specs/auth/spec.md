# auth Specification

## Purpose

Provides authentication and authorization for the API: password hashing, admin seeding, JWT login and validation, and server-side enforcement of User/Admin role boundaries.

## Requirements

### Requirement: Password hashing

The system SHALL hash passwords using BCrypt with a per-password salt, a configurable work factor, constant-time verification, and BCrypt's self-describing stored format containing the algorithm version, salt, and work factor. Plaintext passwords SHALL never be persisted or logged.

#### Scenario: Hash and verify a password

- **WHEN** a password is hashed and then verified with the correct plaintext
- **THEN** verification succeeds

#### Scenario: Incorrect password rejected

- **WHEN** a hash is verified against the wrong plaintext
- **THEN** verification fails

#### Scenario: Stored format carries algorithm parameters

- **WHEN** a hash is produced
- **THEN** the stored BCrypt value encodes the algorithm version, salt, and work factor, and can be verified independently of any in-memory state

### Requirement: Admin user seed

The system SHALL create the first admin user from `.env` (`ADMIN__USERNAME` / `ADMIN__PASSWORD`) on startup only when no admin user exists, hashing the configured password. The seed SHALL be idempotent across restarts and SHALL not reset credentials or duplicate the account.

#### Scenario: First startup seeds admin

- **WHEN** the system starts and no admin user exists
- **THEN** an admin user is created with the hashed configured password

#### Scenario: Restart does not duplicate or reset

- **WHEN** the system starts and an admin user already exists
- **THEN** no account is duplicated and existing credentials are unchanged

#### Scenario: Missing or invalid seed settings

- **WHEN** the configured seed settings are missing or invalid
- **THEN** startup fails deterministically without persisting or logging the plaintext password

### Requirement: Login

The system SHALL expose `POST /api/auth/login` that accepts `{username, password}` and, on success, returns `{token, username, role, expiresAt}` where the token is an 8-hour HS256 JWT carrying the username, role, issuer, and audience. On invalid credentials the system SHALL return an indistinguishable 401.

#### Scenario: Valid login returns token

- **WHEN** a request provides valid credentials
- **THEN** the response returns a token, username, role, and expiry

#### Scenario: Invalid credentials rejected

- **WHEN** a request provides an unknown username or wrong password
- **THEN** the response is a 401 indistinguishable between the two failure causes

#### Scenario: Token lifetime and claims

- **WHEN** a token is issued
- **THEN** it expires 8 hours after issuance and contains the username, role, issuer, and audience claims

### Requirement: Authorization policies

The system SHALL require authentication for every business endpoint except `POST /api/auth/login`, SHALL require the Admin role for all `/api/admin/*` endpoints, and SHALL allow anonymous access to the operational `/health` and `/health/live` endpoints used by container orchestration.

#### Scenario: Anonymous request rejected

- **WHEN** an unauthenticated request is made to a non-login business endpoint
- **THEN** the request is rejected with 401

#### Scenario: Operational health probe allowed

- **WHEN** an unauthenticated request is made to `/health` or `/health/live`
- **THEN** authorization does not block the health response

#### Scenario: User denied admin endpoint

- **WHEN** an authenticated User requests an `/api/admin/*` endpoint
- **THEN** the request is rejected with 403

#### Scenario: Admin allowed admin endpoint

- **WHEN** an authenticated Admin requests an `/api/admin/*` endpoint
- **THEN** the request is allowed

### Requirement: Token hardening

The system SHALL reject tokens that are expired, tampered, use an unexpected algorithm, or carry an unexpected issuer or audience, accounting for a bounded clock skew and validating that the JWT secret meets a minimum length. Auth-related logging SHALL not include secrets.

#### Scenario: Expired token rejected

- **WHEN** a request presents an expired token
- **THEN** the request is rejected with 401

#### Scenario: Tampered token rejected

- **WHEN** a request presents a token whose signature does not validate
- **THEN** the request is rejected with 401

#### Scenario: Wrong algorithm rejected

- **WHEN** a request presents a token signed with an algorithm other than HS256
- **THEN** the request is rejected with 401

#### Scenario: Wrong issuer or audience rejected

- **WHEN** a request presents a validly-signed token with a different issuer or audience
- **THEN** the request is rejected with 401
