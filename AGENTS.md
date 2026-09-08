# Repository Guide

## Sources Of Truth

- Use `ProjectDocs/PRD.md` and `ProjectDocs/Technical-Specification.md` as the primary source for product requirements and target architecture.
- Use `openspec/specs/` for accepted, implemented capabilities and `openspec/changes/archive/` for change history. There are currently no active changes outside `archive/`.
- Reconcile documentation with executable code before editing. The technical specification describes the target system and is ahead of the repository: the current solution is .NET 10 and has no Worker, frontend, RabbitMQ, auth, or normalized IOC model yet.

## Current Architecture

- `ThreatIntelligence.slnx` contains API, Application, Domain, Infrastructure, and one xUnit test project. Despite the solution's virtual `/src/` folder, production projects live at the repository root.
- Keep dependencies directed as `Domain <- Application <- Infrastructure <- API`. Domain owns entities; Application owns DTOs, abstractions, MediatR commands/handlers, and pipeline behavior; Infrastructure owns EF Core, provider clients, and implementations; API owns controllers and middleware.
- Controllers depend on `IMediator`, return typed `ActionResult<T>`, and declare `ProblemDetails` error responses. Do not inject Infrastructure provider services into controllers or add controller-level exception handling.
- Current persistence intentionally stores provider responses in typed `providers.ThreatFox` and `providers.PhishTank` tables. The normalized `Iocs`/`IocSources` model in `ProjectDocs/` is planned, not currently implemented.

## Commands

- Restore/build: `dotnet restore ThreatIntelligence.slnx` then `dotnet build ThreatIntelligence.slnx --no-restore`.
- Run all tests: `dotnet test ThreatIntelligence.slnx`.
- Run the test project: `dotnet test tests/ThreatIntelligence.Application.Tests/ThreatIntelligence.Application.Tests.csproj`.
- Run one class or method: `dotnet test tests/ThreatIntelligence.Application.Tests/ThreatIntelligence.Application.Tests.csproj --filter "FullyQualifiedName~DatabaseConnectionStringResolverTests"` (replace the suffix with the target class or method).
- The API checks SQL Server during startup and terminates if it cannot connect. For a fresh local database, use `docker compose up -d sqlserver`, then `dotnet ef database update -p ThreatIntelligence.Infrastructure -s ThreatIntelligence.API`; migrations are never applied automatically.
- Run the API locally with `dotnet run --project ThreatIntelligence.API` (HTTP launch profile: `http://localhost:5278`). Full Compose exposes the API at `http://localhost:8080`.

## Configuration Gotchas

- Root `.env` supplies `SA_PASSWORD`, `AuthKey__AbuseCh`, and `PhishTank__ApiKey`; `Program.cs` loads that root file explicitly for local runs, while Compose injects the same names. Never read, print, or commit `.env` or ignored `appsettings.Development.json` values.
- `ThreatIntelligence.API/appsettings.json` must retain the `__DB_PASSWORD__` placeholder. `DatabaseConnectionStringResolver` replaces only that token from `SA_PASSWORD`; a missing password intentionally reaches the startup health check as an authentication failure.
- EF migrations belong in `ThreatIntelligence.Infrastructure/Migrations`; always specify Infrastructure as `-p` and API as `-s` in EF commands.
