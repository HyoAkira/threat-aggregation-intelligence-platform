## Why

The platform already fetches IOCs from ThreatFox (abuse.ch). To build a complete
threat intelligence aggregation system, we also need phishing URL data from
PhishTank. The `providers.PhishTank` table, entity class, entity configuration,
and database migration (with unique index on `PhishId`) already exist with all
typed columns — but no code exists to populate it. The PhishTank data API serves
the online-valid dataset without requiring an API key at
`http://data.phishtank.com/data/online-valid.json.gz`. This change implements
the download, parsing, and persistence layer for the PhishTank online-valid
dataset, following the same pattern established by `ThreatFoxApiService`.

## What Changes

- **New**: `PhishTankApiService.cs` in `ThreatIntelligence.Infrastructure/Services/`
  — service that downloads `online-valid.json.gz` from
  `http://data.phishtank.com/data/online-valid.json.gz`, decompresses the gzip
  stream, parses the JSON array of phish entries, and upserts each record into
  `providers.PhishTank` via SQL Server MERGE on `PhishId`.
- **New**: `DownloadDatasetAsync()` method on `PhishTankApiService` — fetches the
  full online-valid dataset (all verified, online phishing entries), decompresses
  gzip, parses JSON, upserts records, returns count processed.
- **New**: `GET /api/phishtank/download` endpoint in a new `PhishTankController.cs`
  in `ThreatIntelligence.API/Controllers/` — triggers `DownloadDatasetAsync()` and
  returns phish entry count; returns 502 on upstream API errors.
- **Update**: `Infrastructure/DependencyInjection.cs` — register
  `PhishTankApiService` with `HttpClient` configured with base address
  `http://data.phishtank.com/data/`.

## Capabilities

### New Capabilities
- `phishtank-dataset-download`: Downloading the PhishTank online-valid dataset
  `providers.PhishTank` table with upsert semantics on `PhishId`.

### Modified Capabilities
- `persistence/ef-core-dbcontext`: Adding `PhishTankApiService` registration
  alongside existing service registrations in `DependencyInjection.cs`.
- `persistence/provider-schema`: No schema changes — the `providers.PhishTank`
  table already exists with all columns and unique constraint on `PhishId`.
  This change implements the fetching/persistence that was specified but not yet
  implemented.

## Impact

- **`ThreatIntelligence.Infrastructure` project**: New
  `PhishTankApiService.cs` file; updated `DependencyInjection.cs` to register the
  service with HttpClient.
- **`ThreatIntelligence.API` project**: New   `PhishTankController.cs` with `/api/phishtank/download` endpoint.
- **Database**: No schema changes — `providers.PhishTank` table already exists
  with all columns and unique constraint on `PhishId`.
- **Configuration**: No API key required — PhishTank's data endpoint serves
  `online-valid.json.gz` without authentication.
- **Dependencies**: Uses only .NET built-in libraries —
  `System.Net.Http`, `System.IO.Compression`, `System.Text.Json` (no new NuGet
  packages).
- **No breaking changes** — adds new functionality without modifying existing
  code paths.
