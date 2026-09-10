## 1. Admin API

- [ ] 1.1 Implement the Admin provider dashboard and verify modes, status, times, never-run, and role tests (TI-100).
- [ ] 1.2 Implement provider history and logical-job status with bounded paging; verify newest-first, 400, and 404 cases (TI-101).
- [ ] 1.3 Implement distinct grouped summary counts and verify cross-provider and port variants count once (TI-102).
- [x] 1.4 Implement non-secret settings read/update atomically with 30-second application; verify invalid requests write nothing (TI-103).
- [ ] 1.5 Implement configured-file log tailing with level/count filters; verify rotation, malformed lines, bounds, and path safety (TI-105).
- [ ] 1.6 Emit correlated secret-safe fetch lifecycle events; verify one job is traceable across API and Worker (TI-106).
- [ ] 1.7 Verify the full build and Admin authorization, SQL, settings, and log integration tests pass.
