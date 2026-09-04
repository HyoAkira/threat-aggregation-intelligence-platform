# Threat Aggregation Intelligence Platform

A .NET 10 Clean Architecture platform that fetches threat intelligence from
multiple sources (ThreatFox, PhishTank), stores raw API responses, and
provides search/observability endpoints.

## Architecture

```
Domain ← Application ← Infrastructure ← API
```

- **Domain**: Entity definitions, no framework dependencies
- **Application**: Use cases and abstractions
- **Infrastructure**: Data persistence (EF Core) + provider data models
- **API**: ASP.NET Core web layer

### Database

- **Connection**: SQL Server via `DefaultConnection` string in `appsettings.json`
- **Migrations**: Applied manually with `dotnet ef database update` — no auto-migrate at startup
- **Schemas and tables**:

  | Table | Schema | Columns |
  |-------|--------|---------|
  | `Provider` | `threat` | ProviderId, Name, BaseUrl, IsEnabled, FetchIntervalMinutes |
  | `ThreatFox` | `providers` | 15 typed columns: IocId, IocValue, IocType, ThreatType, Malware, MalwareAlias, MalwarePrintable, FirstSeenUtc, LastSeenUtc, ConfidenceLevel, IsCompromised, Reference, Tags, Anonymous, Reporter |
  | `PhishTank` | `providers` | 9 typed columns: PhishId, Url, PhishDetailUrl, SubmissionTime, Verified (bit), VerificationTime, Online (bit), Target, Details (JSON) |

- **Unique constraints** (prevent duplicate inserts on re-fetch):
  - `providers.ThreatFox`: `(IocId, IocValue, IocType)`
  - `providers.PhishTank`: `PhishId`
- **No API keys in DB** — secrets read from user-secrets at runtime

## Prerequisites

- .NET 10 SDK
- Docker + Docker Compose
- SQL Server (`mcr.microsoft.com/mssql/server:2025-latest`)

## Getting Started

### 1. Configure secrets

```bash
# Docker — copy .env.template and fill in values
cp .env.template .env
# Set SA_PASSWORD, AuthKey__AbuseCh, PhishTank__ApiKey

# Local dev — use dotnet user-secrets
cd ThreatIntelligence.API
dotnet user-secrets set "AuthKey__AbuseCh" "your-key"
dotnet user-secrets set "PhishTank__ApiKey" "your-key"
```

### 2. Start services

```bash
docker compose up --build
# SQL Server: localhost:1433
# API: http://localhost:8080
```

### 3. Apply database migrations

```bash
dotnet ef database update \
  -p ThreatIntelligence.Infrastructure \
  -s ThreatIntelligence.API
```

## Git-ignored files

- `.env` — Docker secrets; **never commit**
- `appsettings.Development.json` — local overrides
- `dotnet user-secrets` store — API keys never stored in DB or source control
- See `.env.template` for required variables
