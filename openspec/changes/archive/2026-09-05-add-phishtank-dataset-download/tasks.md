## 1. PhishTankApiService

- [x] 1.1 Create `PhishTankApiService.cs` in `ThreatIntelligence.Infrastructure/Services/` with `DownloadDatasetAsync()` using `HttpClient`, `GzipStream`, and `JsonDocument` — verified: file compiles, `dotnet build` succeeds
- [x] 1.2 Implement `DownloadDatasetAsync()`: fetch `online-valid.json.gz` from `http://data.phishtank.com/data/online-valid.json.gz`, decompress gzip, parse JSON array, upsert each entry via MERGE on `PhishId` — verified: method returns count of records processed (build succeeds, MERGE SQL matches existing provider-schema pattern)
- [x] 1.3 Implement `ParseJsonArray()` for JSON array parsing: enumerate root array, map each element to a `PhishTank` entity with "yes"/"no" → bool conversion for `Verified` and `Online` — verified: all 10 fields from JSON map to the correct entity columns per spec table
- [x] 1.4 Implement upsert via SQL Server `MERGE` on `PhishId` — verified: MERGE handles INSERT (new PhishId) and UPDATE (existing PhishId) via SQL MERGE statement

## 2. Dependency Injection

- [x] 2.1 Register `PhishTankApiService` as a scoped service and configure `HttpClient` with base address `http://data.phishtank.com/data/` in `Infrastructure/DependencyInjection.cs` — verified: `builder.Services.AddHttpClient<PhishTankApiService>` added alongside `ThreatFoxApiService`

## 3. API Controller

- [x] 3.1 Create `PhishTankController.cs` in `ThreatIntelligence.API/Controllers/` with `GET /api/phishtank/download` endpoint — verified: endpoint returns HTTP 200 with phish entry count, build succeeds
- [x] 3.2 Return appropriate HTTP 502 for upstream API errors — verified: `catch (HttpRequestException ex)` returns `StatusCode(502, ...)`

## 4. Verification

- [x] 4.1 Build the solution (`dotnet build`) and fix any compilation errors — verified: `dotnet build` succeeds with 0 errors (5 warnings, all pre-existing: OpenApi vulnerability, nullability CS8620 on MERGE params matching ThreatFox pattern, ASP0000 on Program.cs)
