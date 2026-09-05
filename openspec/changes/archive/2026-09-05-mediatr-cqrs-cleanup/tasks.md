## 1. Response DTOs

- [x] 1.1 Create `Application/DTOs/DownloadDatasetResponse.cs` as an immutable record with `RecordsProcessed`, `Message`, `FetchedAtUtc` (Record, DateTime) — verify: build succeeds and file exists with no public setters
- [x] 1.2 Create `Application/DTOs/GetRecentIoCsResponse.cs` as an immutable record with `RecordsProcessed`, `Message`, `FetchedAtUtc` — verify: build succeeds and file exists with no public setters

## 2. Service Abstractions and Infrastructure

- [x] 2.1 Update `IThreatFoxApiService` and `IPhishTankApiService` return types to `Task<DownloadDatasetResponse>` and `Task<GetRecentIoCsResponse>` — verify: solution compiles after abstraction change (drives downstream updates)
- [x] 2.2 Update `ThreatFoxApiService` to construct and return `DownloadDatasetResponse` / `GetRecentIoCsResponse` (Message `"Download completed"` / `"Recent IOCs fetched"`, `FetchedAtUtc = DateTime.UtcNow`) — verify: solution compiles
- [x] 2.3 Update `PhishTankApiService` to construct and return `DownloadDatasetResponse` (Message `"Download completed"`) — verify: solution compiles

## 3. MediatR Commands and Handlers

- [x] 3.1 Update `DownloadThreatFoxDatasetCommand`, `GetRecentThreatFoxIoCsCommand`, `DownloadPhishTankDatasetCommand` to `IRequest<DownloadDatasetResponse>` / `IRequest<GetRecentIoCsResponse>` — verify: solution compiles
- [x] 3.2 Update all three handlers to pass through the DTO returned by the service — verify: solution compiles and handler return types match commands

## 4. Controllers

- [x] 4.1 Update `ThreatFoxController` actions to return the typed DTO directly (no anonymous object) — verify: solution compiles and JSON field `RecordsProcessed` is returned
- [x] 4.2 Update `PhishTankController` to return the typed DTO directly (no anonymous object) — verify: solution compiles and JSON field `RecordsProcessed` is returned

## 5. Namespace Fixes

- [x] 5.1 Change `ThreatIntelligence.Infrastructure/DependencyInjection.cs` and `ThreatIntelDbContext.cs` to namespace `ThreatIntelligence.Infrastructure` and fix any referencing usings — verify: solution compiles

## 6. Tests

- [x] 6.1 Create `tests/ThreatIntelligence.Application.Tests` xunit project and add it to `ThreatIntelligence.slnx` — verify: `dotnet sln ThreatIntelligence.slnx list` includes the test project
- [x] 6.2 Add unit tests asserting DTO construction, immutability (no setter), and value equality — verify: `dotnet test` passes

## 7. Integration Verification

- [x] 7.1 Verify full build and test suite green: `dotnet build ThreatIntelligence.slnx` and `dotnet test` — verify: exit code 0
- [x] 7.2 Run `openspec validate mediatr-cqrs-cleanup` — verify: change is valid