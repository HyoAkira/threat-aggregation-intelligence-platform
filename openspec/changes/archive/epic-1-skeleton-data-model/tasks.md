## 1. Domain model

- [ ] 1.1 Add `IocType` enum (`IPv4`, `Domain`, `URL`) to `ThreatIntelligence.Domain`
- [ ] 1.2 Add `Ioc` entity (`IocId`, `Type`, `NormalizedValue`, `CreatedAt`, `UpdatedAt`); no duplicate `(Type, NormalizedValue)` — verify by attempting a duplicate insert and asserting a DB constraint exception
- [ ] 1.3 Add `IocSource` entity (`IocSourceId`, `IocId`, `ProviderId`, `FirstSeen`, `LastSeen`, `RawValue`, `Metadata?`)
- [ ] 1.4 Add `Provider` entity (`ProviderId`, `Name`, `BaseUrl`, `FetchIntervalMinutes`, `Enabled`, `SupportedTypes`)
- [ ] 1.5 Add `FetchHistory` entity (`FetchHistoryId`, `ProviderId`, `StartedAt`, `CompletedAt`, `Status`, `CountFetched`, `CountUpserted`, `Error?`)
- [ ] 1.6 Add `LogEntry` entity (`LogEntryId`, `Timestamp`, `Level`, `Category`, `Message`, `Exception?`)

## 2. EF Core + infrastructure

- [ ] 2.1 Add EF Core + Tools NuGet packages to `ThreatIntelligence.Infrastructure` and the startup project (`ThreatIntelligence.API`)
- [ ] 2.2 Add `IThreatIntelDbContext` interface in `ThreatIntelligence.Application` exposing `DbSet<Ioc>`, `DbSet<IocSource>`, `DbSet<Provider>`, `DbSet<FetchHistory>`, `DbSet<LogEntry>`
- [ ] 2.3 Implement `ThreatIntelDbContext` in Infrastructure, registering `DbSet`s and `AddInfrastructure` wiring it via `AddDbContext<IThreatIntelDbContext, ThreatIntelDbContext>(..., sql)`
- [ ] 2.4 Add Fluent `EntityTypeConfiguration` for all five entities: `NormalizedValue` unique index on `(Type, NormalizedValue)`; `IocType` string conversion; `IocSource.Metadata` JSON column; cascade rules
- [ ] 2.5 Seed `Provider` (ThreatFox, PhishTank, URLhaus) via `HasData` with stable GUIDs
- [ ] 2.6 Author `Initial` migration (`dotnet ef migrations add Initial` from API project) and verify it creates 5 tables + unique index + 3 seed rows

## 3. Application CQRS + validation skeleton

- [ ] 3.1 Register MediatR (`AddMediatR`) + FluentValidation (`AddValidatorsFromAssembly`) in `AddApplication`
- [ ] 3.2 Add `PingDto` + `PingQuery : IRequest<PingDto>` in Application with a `PingQueryValidator`
- [ ] 3.3 Implement `PingQueryHandler` depending only on `IThreatIntelDbContext` (returns `{ ok: true, providerCount: N }`); verify `dotnet build` is green

## 4. API wiring + smoke endpoint

- [ ] 4.1 Register MediatR + FluentValidation pipeline in `Program.cs` (before `MapControllers`)
- [ ] 4.2 Add `IEndpointRouteBuilder` extension mapping `GET /api/ping` to the pipeline via `MediatR`; `docker compose up` not yet required — hit `dotnet run` and `GET /api/ping` returns `{ ok: true, providerCount: 3 }`
- [ ] 4.3 Add `Database.Migrate()` first-run provisioning at app startup; verify the DB is created on first boot with the seed providers present

## 5. Docker + secrets

- [ ] 5.1 Add `docker-compose.yml` (`sqlserver` mssql 2022 image with `ACCEPT_EULA` + `SA_PASSWORD` from `.env`, plus `api` service building from `ThreatIntelligence.API`, env-driven connection string + API keys)
- [ ] 5.2 Create gitignored `.env.template` documenting `SA_PASSWORD`, `AuthKey__AbuseCh`, `PhishTank__ApiKey`; add `.env` + `appsettings.Development.json` to `.gitignore`
- [ ] 5.3 Configure `dotnet user-secrets` locally for the API project (`AuthKey__AbuseCh`, `PhishTank__ApiKey`); verify `dotnet user-secrets list` shows both keys and `git status` commits neither

## 6. Verification

- [ ] 6.1 `dotnet build` compiles all four projects with no warnings-as-errors
- [ ] 6.2 `docker compose up --build` starts both containers and `GET /api/ping` returns `providerCount: 3`
- [ ] 6.3 Inspect `Provider` table in the SQL container: 3 seeded rows, each enabled
- [ ] 6.4 `openspec validate --change epic-1-skeleton-data-model` reports the change complete (proposal, specs, design, tasks all present)
