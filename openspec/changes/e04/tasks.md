## 1. Password hashing service

- [ ] 1.1 Add `IPasswordHasher` abstraction in Application and verify it compiles
- [ ] 1.2 Add the maintained `BCrypt.Net-Next` package and implement BCrypt hashing in Infrastructure with an explicit configurable work factor; verify the stored hash embeds the algorithm version, salt, and work factor
- [ ] 1.3 Verify hash/verify unit tests pass: correct password succeeds, wrong password fails
- [ ] 1.4 Verify no plaintext is logged or persisted during hashing (inspect logs and stored value)

## 2. Admin user seed

- [ ] 2.1 Add startup admin-seed service in Infrastructure reading `ADMIN__USERNAME`/`ADMIN__PASSWORD` and verify it creates one admin only when none exists
- [ ] 2.2 Make seed idempotent and race-safe (unique index on `users.username`, guarded insert) and verify restart does not duplicate or reset credentials
- [ ] 2.3 Add deterministic startup failure for missing/invalid seed settings and verify no plaintext is logged

## 3. Login command and JWT

- [ ] 3.1 Add `Login` MediatR command/handler resolving the user by username and verifying the hash in constant time
- [ ] 3.2 Add JWT token service generating 8-hour HS256 tokens with username/role/issuer/audience claims
- [ ] 3.3 Add `POST /api/auth/login` endpoint returning `{token, username, role, expiresAt}` and verify valid login returns the contract
- [ ] 3.4 Make invalid credentials (unknown user vs wrong password) return indistinguishable 401 and verify both cases behave identically
- [ ] 3.5 Add config validation (`JWT__SECRET` ≥32 bytes, issuer, audience, expiry default 480) and verify missing/invalid config fails fast without logging secrets

## 4. Authorization policies

- [ ] 4.1 Register JWT bearer authentication as default and a fallback policy requiring authentication, with `POST /api/auth/login` marked `[AllowAnonymous]`
- [ ] 4.2 Add `Admin` role policy and apply it to admin controllers (`/api/admin/*`)
- [ ] 4.3 Verify integration matrix on SQL Server: anonymous=401, User-on-admin=403, Admin=success

## 5. Token hardening

- [ ] 5.1 Configure `TokenValidationParameters`: HS256-only algorithm, issuer/audience, bounded clock skew, lifetime validation
- [ ] 5.2 Add token unit tests and verify expired, tampered, wrong-algorithm, wrong-issuer, and wrong-audience tokens are all rejected with 401
- [ ] 5.3 Add safe auth logging (no secrets, no full tokens) and verify logs contain no credentials

## 6. Configuration and wiring

- [ ] 6.1 Update `.env.example` with non-secret placeholders for `JWT__SECRET`, `JWT__ISSUER`, `JWT__AUDIENCE`, `JWT__EXPIRYMINUTES`, `ADMIN__USERNAME`, `ADMIN__PASSWORD`
- [ ] 6.2 Register auth/hashing services in API DI and verify the full build and test suite pass
