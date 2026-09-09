## 1. Authentication

- [ ] 1.1 Implement BCrypt hashing and activate first-admin seeding; verify hash, restart, conflict, and secret-safety tests (TI-040, FR-001).
- [ ] 1.2 Implement login with a fixed 8-hour HS256 JWT; verify success and indistinguishable invalid credentials (TI-041).
- [ ] 1.3 Require authentication by default and Admin for `/api/admin/*`; verify 401, 403, and success cases (TI-042).
- [ ] 1.4 Enforce secret length, issuer, audience, algorithm, expiry, and clock skew; verify invalid-token tests (TI-043).
- [ ] 1.5 Update non-secret config examples and verify the full build plus unit and SQL-backed auth tests pass.
