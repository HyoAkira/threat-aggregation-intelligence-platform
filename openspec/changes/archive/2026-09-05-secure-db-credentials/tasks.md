## 1. Credential Resolution

- [x] 1.1 Create `Infrastructure/ConnectionStrings/DatabaseConnectionStringResolver.cs` with a static `Resolve(string? configured, string? password)` that replaces the `__DB_PASSWORD__` token — verify: solution compiles
- [x] 1.2 Update `AddInfrastructure` to resolve the connection string via the resolver using `Environment.GetEnvironmentVariable("SA_PASSWORD")` — verify: solution compiles

## 2. Configuration

- [x] 2.1 Replace the hardcoded password in `ThreatIntelligence.API/appsettings.json` with `Password=__DB_PASSWORD__` placeholder — verify: no real password string remains in the file (grep shows only placeholder)

## 3. Tests

- [x] 3.1 Add tests for `DatabaseConnectionStringResolver`: token replaced, unset password yields empty password, non-token input unchanged — verify: `dotnet test` passes

## 4. Integration Verification

- [x] 4.1 Verify `docker-compose.yml` already drives passwords via `${SA_PASSWORD}` (no edits) — verify: grep shows env interpolation for both sqlserver and api
- [x] 4.2 Verify `dotnet build ThreatIntelligence.slnx` and `dotnet test` both green — verify: exit code 0
- [x] 4.3 Run `openspec validate secure-db-credentials` — verify: change is valid