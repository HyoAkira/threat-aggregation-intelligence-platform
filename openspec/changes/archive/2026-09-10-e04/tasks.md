## 1. Authentication

- [x] 1.1 Implement BCrypt hashing with a configurable work factor and self-describing hashes (TI-040, FR-001).
- [x] 1.2 Seed the first admin with a BCrypt hash and verify restart, conflict, race, and plaintext-persistence behavior (TI-040).
- [x] 1.3 Make unknown-user and wrong-password login paths perform password verification and return the same 401 contract (TI-041).
- [x] 1.4 Issue an HS256 JWT with username, role, issuer, audience, and a fixed 8-hour expiry (TI-041).
- [x] 1.5 Require authentication for business endpoints and the Admin role for `/api/admin/*` (TI-042).
- [x] 1.6 Keep `/health` and `/health/live` anonymous for container probes (TI-042).
- [x] 1.7 Reject expired, tampered, wrong-algorithm, wrong-issuer, and wrong-audience tokens with bounded clock skew (TI-043).
- [x] 1.8 Verify login and authorization through the API against SQL Server, including 401, 403, and Admin success (TI-041, TI-042).
- [x] 1.9 Verify auth logs and persisted values do not contain plaintext passwords or full tokens (TI-040, TI-043).
- [x] 1.10 Validate non-secret configuration examples, OpenSpec artifacts, the full build, unit tests, and integration tests.
