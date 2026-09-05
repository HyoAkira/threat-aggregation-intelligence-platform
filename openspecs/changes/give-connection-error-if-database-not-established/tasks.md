## 1. DatabaseHealthService (Infrastructure)

- [x] 1.1 Create `ThreatIntelligence.Infrastructure/Services/DatabaseHealthService.cs` with a `CanConnectAsync()` method that uses `ThreatIntelDbContext.Database.CanConnectAsync()` and exposes the configured server host for diagnostics.
- [x] 1.2 Register `DatabaseHealthService` as a scoped service in `ThreatIntelligence.Infrastructure/DependencyInjection.cs`.

## 2. Startup fail-fast wiring (API)

- [x] 2.1 In `ThreatIntelligence.API/Program.cs`, after `AddInfrastructure(...)` and before `builder.Build()`, build a scope, resolve `DatabaseHealthService`, and call `CanConnectAsync()`.
- [x] 2.2 On failure: write a clear message (connection target = server host, no credentials) to `Console.Error` and terminate via `Environment.FailFast(...)`.

## 3. Verification

- [x] 3.1 `dotnet build` succeeds with 0 errors.
- [x] 3.2 With the database reachable, `dotnet run` starts the application normally ("Application started"; EF `SELECT 1` seen before Kestrel bind).
- [x] 3.3 With the database unreachable, the application terminates during startup (`Database connection cannot be established to localhost,1433.` → exit 134, Kestrel never started).
