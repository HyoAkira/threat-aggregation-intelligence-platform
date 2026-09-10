## Why

The PRD (FR-001, §4) requires that every non-login endpoint be protected and that User/Admin role boundaries be enforced server-side. No authentication exists yet: there is no password hashing, no login, and no JWT issuance or validation, so today every API endpoint would be unprotected and role enforcement would be impossible.

## What Changes

- Add a password hashing service using BCrypt with a per-password salt, configurable work factor, constant-time verification, and a stored format that carries algorithm parameters for future upgrades.
- Add a login command and `POST /api/auth/login` endpoint that issues an 8-hour HS256 JWT containing username, role, expiry, issuer, and audience; invalid credentials return an indistinguishable 401.
- Seed the first admin user from `.env` (`ADMIN__USERNAME` / `ADMIN__PASSWORD`) when no admin exists, hashing the password without ever persisting or logging plaintext.
- Configure authorization policies so authentication is required by default for business endpoints, the Admin role is required for `/api/admin/*`, and operational health probes remain anonymous.
- Harden token handling: validate secret length, clock skew, algorithms, issuer/audience, expiration, and safe auth logging.

## Capabilities

### New Capabilities

- `auth`: password hashing, login/JWT issuance, admin seeding, and token validation for the User/Admin role model.

### Modified Capabilities

- None — `openspec/specs/` is empty; all capabilities are new.

## Impact

- `src/ThreatIntelligence.Domain` — `User` and `UserRole` already exist (E02); no entity changes expected unless the stored hash format requires a type adjustment.
- `src/ThreatIntelligence.Application` — password hashing service, login command/handler, and auth-related abstractions.
- `src/ThreatIntelligence.Infrastructure` — JWT token generation/validation implementation and admin-user seed logic.
- `src/ThreatIntelligence.API` — `POST /api/auth/login` endpoint, authorization policy configuration (auth-by-default, Admin on `/api/admin/*`), and JWT bearer validation wiring.
- `src/ThreatIntelligence.Worker` — no changes expected (auth is API-side), unless shared startup config requires the same JWT validation settings.
- `tests/ThreatIntelligence.UnitTests` — hashing, token, and policy unit tests.
- `tests/ThreatIntelligence.IntegrationTests` — login, seed, and authorization-matrix tests against real persistence (SQL Server).
- Configuration keys: `JWT__SECRET` (≥32 bytes), `JWT__ISSUER`, `JWT__AUDIENCE`, `ADMIN__USERNAME`, and `ADMIN__PASSWORD`; `.env.example` is updated with non-secret placeholders. Token lifetime is fixed at 8 hours for the MVP.
