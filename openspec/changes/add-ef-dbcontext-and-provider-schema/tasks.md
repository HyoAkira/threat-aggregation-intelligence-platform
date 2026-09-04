## 1. Domain model

- [x] 1.1 Add `Provider` entity to `ThreatIntelligence.Domain` with `ProviderId` (Guid, PK), `Name` (string), `BaseUrl` (string), `IsEnabled` (bool), `FetchIntervalMinutes` (int) — verify by inspecting the class definition and relationship to Infrastructure
- [x] 1.2 Remove placeholder `Class1.cs` from Domain (no longer needed) — verify by `dotnet build` passing with no dangling references

## 2. Infrastructure — DbContext + configurations

- [x] 2.1 Add `ThreatIntelDbContext` to Infrastructure with `DbSet<Provider>`, `DbSet<ThreatFox>`, `DbSet<PhishTank>` — verify by `dotnet build` compiling
- [x] 2.2 Add `ProviderEntityConfiguration` mapping to `Provider` table in `threat` schema with PK — verify by inspecting the `EntityTypeConfiguration` class
- [x] 2.3 Add `ThreatFoxEntityConfiguration` mapping to `providers.ThreatFox` with all typed columns and unique constraint `(IocId, IocValue, IocType)` — verify by inspecting columns + constraints in the configuration
- [x] 2.4 Add `PhishTankEntityConfiguration` mapping to `providers.PhishTank` with typed columns (`Verified`/`Online` as `bit`, `Details` as JSON) and unique constraint on `PhishId` — verify by inspecting columns + constraints
- [x] 2.5 Wire `AddInfrastructure` to register `ThreatIntelDbContext` with SQL Server provider and `DefaultConnection` string — verify by `dotnet build`
- [x] 2.6 Remove placeholder `Class1.cs` from Infrastructure — verify by `dotnet build` with no dangling references
- [x] 2.7 Author `Initial` EF migration (`dotnet ef migrations add Initial` from API project) — verify by inspecting the generated migration creates `threat.Provider`, `providers.ThreatFox`, `providers.PhishTank` tables + unique indexes

## 3. API — connection string + startup

- [x] 3.1 Add `ConnectionStrings:DefaultConnection` to `appsettings.json` pointing at the local SQL Server container (`Server=localhost,1433;Database=ThreatIntel;User Id=sa;Password=REDACTED;TrustServerCertificate=True;`) — verify by file inspection
- [x] 3.2 Confirm `Program.cs` does NOT call `Database.Migrate()` — migrations are applied manually — verify by code inspection
- [x] 3.3 Confirm `ThreatIntelligence.Infrastructure` project reference is present in API csproj (needed for `AddInfrastructure`) — verify by `dotnet build`

## 4. Docker + secrets

- [x] 4.1 Create `docker-compose.yml` with `sqlserver` (mcr.microsoft.com/mssql/server:2025-latest, port 1433, SA password from `.env`) and `api` (builds from `ThreatIntelligence.API/`, env-driven connection string) — verify by `docker compose config`
- [x] 4.2 Create `.env.template` documenting `SA_PASSWORD`, `AuthKey__AbuseCh`, `PhishTank__ApiKey` — verify by file inspection
- [x] 4.3 Update `.gitignore` to exclude `.env` and `appsettings.Development.json` — verify by `git check-ignore .env appsettings.Development.json`
- [x] 4.4 Configure `dotnet user-secrets` for the API project (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) — verify by `dotnet user-secrets list` showing both keys and `git status` not staging secrets

## 5. Verification

- [x] 5.1 `dotnet build` compiles all four projects — verify zero errors
- [x] 5.2 `docker compose up --build` starts both containers and the API boots without error — verify by container logs showing successful startup
- [x] 5.3 Tables exist in `threat.Provider`, `providers.ThreatFox`, `providers.PhishTank` after `dotnet ef database update` — verify by querying the database (tables + schema exist)
- [x] 5.4 `openspec validate --change add-ef-dbcontext-and-provider-schema` reports complete — verify all planning artifacts pass validation
