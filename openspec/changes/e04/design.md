## Context

See proposal.md - Why. The `User` entity and `UserRole` enum already exist in Domain (E02) with `passwordHash nvarchar(256)` and `role tinyint`. The Technical Specification (§2, §4, API §Public) specifies JWT HS256, 8-hour lifetime, no refresh tokens, auth-by-default, Admin-only `/api/admin/*`, and a `.env`-driven admin seed (`ADMIN__USERNAME` / `ADMIN__PASSWORD`). The API project is a .NET 10 Controller-based Web API using MediatR + FluentValidation; no authentication middleware exists yet.

## Goals / Non-Goals

**Goals:**

- A password hashing service using BCrypt whose stored format carries the salt, algorithm version, and work factor for future upgrades.
- JWT login issuing 8-hour HS256 tokens with username/role/issuer/audience claims.
- Idempotent `.env`-driven admin seeding.
- Auth-by-default with Admin required on `/api/admin/*`, proven by an integration matrix.
- Hardened token validation and safe auth logging.

**Non-Goals:**

- No refresh tokens, logout tokens/revocation, or multi-factor (PRD MVP).
- No per-user CRUD or password-change endpoints beyond the seed (future work).
- No frontend auth (login page/state is E11/E12).
- No changes to the `User`/`UserRole` schema shape; seed belongs to this epic, not E02's migration.

## Decisions

- **Password KDF**: Use `BCrypt.Net-Next` wrapped behind an `IPasswordHasher` application abstraction. BCrypt generates a unique per-password salt, embeds the salt and work factor in the stored hash, and provides constant-time verification. Configure the BCrypt work factor explicitly so it can be increased as hardware improves. Rationale: BCrypt is a battle-tested password-specific KDF with a self-describing stored format and straightforward verification; using the maintained NuGet package avoids hand-rolled cryptography.
- **JWT generation/validation**: Use `Microsoft.IdentityModel.JsonWebTokens` (`JsonWebTokenHandler`) with HS256 symmetric signing from `JWT__SECRET` (validated ≥32 bytes at startup), enforcing issuer (`JWT__ISSUER`), audience (`JWT__AUDIENCE`), a bounded clock skew, `TokenValidationParameters` `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]`, and lifetime. Rationale: matches the Tech Spec's chosen library and the "custom validator" (Identity-free) requirement.
- **Login flow**: A `Login` MediatR command/query resolving the user by username, verifying the hash in constant time, and returning `{token, username, role, expiresAt}`. Invalid credentials (unknown user OR wrong password) produce the same 401 to avoid user enumeration. Rationale: consistent with the CQRS/MediatR convention already used in Application.
- **Admin seeding**: A startup service in Infrastructure runs once, inside a scope, and creates the admin from `.env` only when no admin exists, using the same hashing service. Deterministic failure on missing/invalid settings. Rationale: satisfies PRD "first-run admin seeded from `.env`"; guarded to be idempotent and race-safe across concurrent startup.
- **Authorization**: Register JWT bearer authentication as the default, set a fallback policy requiring authenticated users on all controllers, and add an `Admin` policy (role = Admin) applied to admin controllers. `POST /api/auth/login` is `[AllowAnonymous]`. Rationale: auth-by-default with explicit opt-out for login.

## Risks / Trade-offs

- [Default/weak `ADMIN__PASSWORD` in `.env` could ship into production] → Mitigation: startup validation rejects too-short seed password; release checklist (E14) requires changing defaults.
- [Weak or short `JWT__SECRET` weakens tokens] → Mitigation: fail fast on `<32` bytes at startup.
- [Concurrent first-boot seeds could race] → Mitigation: unique index on `users.username` plus idempotent guarded seed; deterministic SQL-backed outcome.
- [Clock skew / environment clock drift rejects valid tokens] → Mitigation: bounded `ClockSkew` in validation, matching Tech Spec's hardening requirement.

## Migration Plan

- Add config keys to `.env.example` (non-secret placeholders) and startup validation.
- Register auth services in API DI and apply authorization policies.
- Deploy/rollback: auth is additive and gated by config; rolling back is removing middleware and the login endpoint. The `users` table migration is owned by E02 and unchanged here.
