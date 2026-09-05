## Context

The application uses .NET (based on the existing `ThreatIntelDbContext` and `dotnet user-secrets` configuration). Currently, if the database is unreachable at startup, the application boots normally and only fails when a database operation is attempted — producing confusing error messages far from the root cause.

See proposal.md — Why for motivation.

## Goals / Non-Goals

**Goals:**
- Fail fast at application startup if the database connection cannot be established
- Log a clear, human-readable error message (without exposing credentials)
- Include the connection target information in the error (host/database) so operators can diagnose connectivity issues

**Non-Goals:**
- Implementing retry logic for transient database failures
- Health check endpoints (HTTP `/health` route) — only startup-time verification
- Changes to the database schema or stored procedures

## Decisions

**Decision 1: Use .NET's built-in health check at startup**
- Use `builder.Services.AddDbContext` with a startup connection check via `IDbContext.Database.CanConnectAsync()` in `Program.cs` before `builder.Build()`.
- Rationale: No new dependencies; follows .NET idioms; runs before Kestrel accepts requests.
- Alternatives considered:
  - **Custom connection wrapper**: Rejected — adds complexity without value.
  - **Hosted service health check**: Rejected — runs after the host starts accepting requests; too late for fail-fast.

**Decision 2: Terminate with a clear error message**
- If `CanConnectAsync()` returns `false` or throws, log the error and call `Environment.FailExit("Database connection cannot be established: <details>")` to terminate before the app serves traffic.
- Rationale: Ensures the process never accepts traffic without a working database.

## Risks / Trade-offs

[Risk] Startup takes longer due to the connection check → Mitigation: Use the configured connection timeout (typically 30 seconds); connection failures surface within that window.

[Risk] False positives if database needs more time to start (e.g., in container orchestrated environments with startup ordering) → Mitigation: Consider making the check configurable via an appsetting flag (`Startup:VerifyDatabaseConnection`) that can be disabled if a reverse dependency like a migration job or external orchestrator manages readiness.

## Open Questions

- Should the connection check be configurable (on/off via appsettings)? This can be decided during task breakdown; it does not change the spec.
