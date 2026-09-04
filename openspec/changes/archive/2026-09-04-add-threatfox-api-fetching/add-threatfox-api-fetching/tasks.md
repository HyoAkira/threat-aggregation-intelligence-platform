## 1. Configuration

- [x] 1.1 Update `.env` with ThreatFox API key and verify the key is readable via `AuthKey__AbuseCh` config — verify by running a test config read in API startup
- [x] 1.2 Remove conflicting user-secrets (`AuthKey__AbuseCh`, `PhishTank__ApiKey`) via `dotnet user-secrets remove` so `.env` takes precedence — verify with `dotnet user-secrets list` (no entries found)
- [x] 1.3 Add `LoadEnvFile()` helper to `Program.cs` that loads `.env` into environment variables before `WebApplication.CreateBuilder()` — pure .NET, no external packages
- [x] 1.4 Verify `.env` is loaded and API key resolved — verified: `/api/threatfox/recent` returns HTTP 200 with 5,977 IOCs processed

## 2. ThreatFoxApiService

- [x] 2.1 Create `ThreatFoxApiService.cs` in `Infrastructure/Services/` with `DownloadDatasetAsync()` and `RecentIoCsAsync()` methods using `HttpClient`, `ZipArchive`, and `JsonDocument` — verify file compiles
- [x] 2.2 Implement `DownloadDatasetAsync()`: fetch `full.json.zip`, decompress, parse JSON object (keyed by ioc_id), upsert each IOC — verified by running and checking 6,000+ DbCommands in log
- [x] 2.3 Implement `RecentIoCsAsync()`: fetch `recent.json`, parse JSON object, upsert each IOC — verified by running and checking 5,977 IOCs processed returned
- [x] 2.4 Implement `MapJsonToEntity()` for JSON-to-entity field mapping — verify all fields from the JSON map to the correct entity columns
- [x] 2.5 Implement upsert via SQL Server `MERGE` on `(IocId, IocValue, IocType)` — verify running twice produces no duplicates

## 3. Dependency Injection

- [x] 3.1 Register `ThreatFoxApiService` as a scoped service and configure `HttpClient` with base address `https://threatfox-api.abuse.ch` in `Infrastructure/DependencyInjection.cs` — verify `builder.Services` contains `ThreatFoxApiService`

## 4. API Controller

- [x] 4.1 Create `ThreatFoxController.cs` in `ThreatIntelligence.API/Controllers/` with `GET /api/threatfox/download` and `GET /api/threatfox/recent` endpoints — verify endpoints return HTTP 200 with IOC count
- [x] 4.2 Return appropriate HTTP status codes for errors (401 for missing API key, 502 for upstream API errors) — verify error responses via curl

## 5. Verification

- [x] 5.1 Build the solution (`dotnet build`) and fix any compilation errors — verify build succeeds
- [x] 5.2 Run `dotnet ef database update` to ensure DB schema is current — verify migrations applied
