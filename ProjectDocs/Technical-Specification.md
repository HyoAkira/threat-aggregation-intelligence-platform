# 🛠️ Technical Specification — Threat Intelligence Aggregator

> Status: Approved for development planning · Companion to `PRD.md`
> Impulse: `/home/hyosuke/Documents/HyoCMS/04 - Archive/PRE-DEVELOPMENT — Project Kickoff Paper.md`

---

## 1. Architecture Overview

```
                         ┌──────────────────────────────┐
   Browser  ───────────▶ │  Frontend  (React SPA / Vite) │
 (user)                  └──────────────┬───────────────┘
                                        │ HTTP (REST / JSON / JWT Bearer)
                                        ▼
                         ┌──────────────────────────────┐
                         │  Api  (.NET 10 Web API, Controllers) │
                         │  • Auth, IOC search, list     │
                         │  • Admin (providers, history,  │
                         │    summary, logs, settings)    │
                         │  • Publishes FetchJob messages │
                         └───────┬──────────────┬────────┘
                                 │ AMQP publish │
                                 ▼              │ REST
                        ┌───────────────┐       │ settings read
                        │   RabbitMQ    │       ▼
                        │ (durable queue│  ┌──────────────────────┐
                        │  + DLX/retry) │  │   MSSQL              │
                        └───────┬───────┘  │  (iocs, providers,   │
                                │ consume  │   fetchHistory, etc.)│
                                ▼          └──────────────────────┘
                        ┌──────────────────────────────┐
                        │  Worker  (.NET BackgroundService)│
                        │  • Scheduler (per-provider)   │
                        │  • Provider adapters          │
                        │  • Run lock, transactions     │
                        └──────────────────────────────┘

External sources: threatfox-api.abuse.ch (Auth-Key), data.phishtank.com (no key)
Deployment: docker-compose → mssql, rabbitmq, api, worker (+ nginx/frontend final phase)
```

**Division of responsibility**

- **Api**: HTTP surface, auth, search/list, admin ops, publishes `FetchJob` messages.
- **Worker**: owns scheduling + fetch execution + DB writing. Polls due providers, executes adapters, one transaction per fetch, acks on success.
- **RabbitMQ**: the job bus between Api and Worker; provides bounded retry and dead-lettering.

---

## 2. Tech Stack Per Area

| Area             | Technology                                                                                    |
| ---------------- | --------------------------------------------------------------------------------------------- |
| Frontend         | React (Vite + TS), Radix UI, Zustand, TanStack Query, Zod                                     |
| Frontend routing | react-router (role-guarded routes)                                                            |
| Backend          | .NET 10 Web API (Controller-based), Clean Architecture + CQRS (**MediatR**), FluentValidation |
| Persistence      | SQL Server 2022, EF Core (code-first), SQL Server via Docker                                  |
| Messaging        | RabbitMQ (via RabbitMQ.Client / MassTransit or bare AMQP)                                     |
| Resilience       | Polly (ephemeral HTTP retries inside adapters), .NET `IHttpClientFactory`                     |
| Logging          | Serilog (Console + rolling file; structured)                                                  |
| Auth             | JWT (HS256), ASP.NET Core Identity-free custom validator + `ClaimsPrincipal`                  |
| Conf/secrets     | dotenv package reading `.env`; ASP.NET configuration layered                                  |
| Tests            | xUnit, FluentAssertions, EF Core InMemory for the pure layers + a SQL-backed integration test |
| Infra            | Docker Compose: `mssql`, `rabbitmq`, `api`, `worker`, (`frontend`/nginx final phase)          |

Example NuGet set: `Microsoft.EntityFrameworkCore.SqlServer`, `MediatR`, `FluentValidation`,
`Polly.Extensions.Http`, `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Microsoft.IdentityModel.JsonWebTokens`,
`DotNetEnv`, `RabbitMQ.Client`.

---

## 3. Enumerations

```
IocType          : tinyint → 0 = Ipv4, 1 = Ipv6, 2 = Domain, 3 = Url
FetchTrigger     : tinyint → 0 = Scheduled, 1 = Manual
FetchMode        : tinyint → 0 = Recent, 1 = Full          (PhishTank only Full)
ProviderStatus   : tinyint → 0 = Running, 1 = Success, 2 = Failed
UserRole         : tinyint → 0 = User, 1 = Admin
```

---

## 4. Database Schema (MSSQL / EF Core)

All physical table and column names use plural lower camel case.

### `providers`
| Column            | Type           | Notes                                                   |
| ----------------- | -------------- | ------------------------------------------------------- |
| id                | int PK         |                                                         |
| name              | nvarchar(255)  | required display/integration name                       |
| baseUrl           | nvarchar(255)  | required                                                |
| isEnabled         | bit            | required; runtime-editable                              |
| intervalMinutes   | int            | required; runtime-editable                              |
| lastFetchStatus   | tinyint?       | ProviderStatus                                          |
| lastFetchAt       | datetime2?     | last completed attempt                                  |
| nextFetchTime     | datetime2?     | computed by worker when a scheduled run completes       |

### `iocs` (one row per provider record)
| Column                   | Type             | Notes                                                        |
| ------------------------ | ---------------- | ------------------------------------------------------------ |
| id                       | int PK           | internal identity                                            |
| providerId               | int FK           | required → providers                                         |
| providerRecordId         | nvarchar(100)    | required provider-native `ioc_id` / `phish_id`                |
| providerDetailsLink      | nvarchar(2048)?  | ThreatFox IOC page or PhishTank `phish_detail_url`            |
| iocValue                 | nvarchar(2048)   | normalized value, see §5                                     |
| iocValueHash             | binary(32)       | persisted SHA-256 of `iocValue` for index-safe identity       |
| iocType                  | tinyint          | IocType                                                       |
| port                     | int?             | explicit endpoint port; CHECK 1–65535                         |
| threatType               | nvarchar(100)?   | ThreatFox                                                     |
| malware                  | nvarchar(200)?   | ThreatFox                                                     |
| malwareAlias             | nvarchar(500)?   | ThreatFox                                                     |
| malwarePrintable         | nvarchar(200)?   | ThreatFox                                                     |
| firstSeenUtc             | datetime2       | ThreatFox `first_seen_utc`; PhishTank `submission_time`       |
| lastSeenUtc              | datetime2?      | ThreatFox `last_seen_utc`; PhishTank `verification_time`      |
| isCompromised            | bit?             | ThreatFox                                                     |
| reporter                 | nvarchar(200)?   | ThreatFox                                                     |
| anonymous                | bit?             | ThreatFox                                                     |
| isVerified               | bit?             | PhishTank                                                     |
| target                   | nvarchar(500)?   | PhishTank                                                     |
| phishTankDetails         | nvarchar(max)?  | validated JSON array retaining every detail field             |
| phishTankOnline          | bit?             | PhishTank                                                     |
| urlHost                  | nvarchar(253)?   | full lowercase URL host, including subdomains                 |
| parentDomain             | nvarchar(253)?   | registrable domain calculated with Public Suffix List rules   |
| threatFoxConfidenceLevel | tinyint?         | ThreatFox; CHECK 0–100                                        |
| createdAt                | datetime2       | preserved across upserts                                     |
| updatedAt                | datetime2       | refreshed on every successful upsert                          |

Provider-inapplicable fields are NULL. Valid `false` and `0` values are not represented as NULL.
PhishTank `phishTankDetails` retains `ipAddress`, `cidrBlock`, `announcingNetwork`, `rir`, and
`detailTime` for every array element; those IPs remain contextual details rather than standalone IOCs.

**Indexes / constraints**
- `UNIQUE (providerId, iocType, iocValueHash, port)` prevents duplicate provider endpoint records.
- `UNIQUE (providerId, providerRecordId)` preserves provider-native record identity.
- `INDEX (iocType, iocValueHash)` supports cross-provider grouping.
- `INDEX (iocType, urlHost)` and `INDEX (iocType, parentDomain)` support URL/domain relations.
- `INDEX (lastSeenUtc)` supports list ordering.
- `CHECK (port IS NULL OR port BETWEEN 1 AND 65535)`.
- `CHECK (threatFoxConfidenceLevel IS NULL OR threatFoxConfidenceLevel BETWEEN 0 AND 100)`.
- `CHECK (phishTankDetails IS NULL OR ISJSON(phishTankDetails) = 1)`.

The API calculates `isFalsePositive` per provider row as
`updatedAt < DATEADD(DAY, -180, SYSUTCDATETIME())`; it is not persisted. A later successful upsert
refreshes `updatedAt`, making the calculated value false.

### `tags` and `iocTags`
| Table   | Columns                                   | Notes                                      |
| ------- | ----------------------------------------- | ------------------------------------------ |
| tags    | id int PK, name nvarchar(100) unique      | tag names are trimmed and lowercased       |
| iocTags | iocId int FK, tagId int FK, composite PK | current provider tags for one `iocs` row   |

An upsert replaces the row's tag associations with the provider's current tag set. Grouped API
responses keep tags under the source that supplied them.

### `fetchHistory`
| Column       | Type             | Notes                                                       |
| ------------ | ---------------- | ----------------------------------------------------------- |
| id           | int PK           |                                                             |
| providerId   | int FK           | required → providers                                        |
| trigger      | tinyint          | FetchTrigger (Scheduled/Manual)                             |
| mode         | tinyint          | FetchMode (Recent/Full)                                     |
| startedAt    | datetime2       |                                                             |
| finishedAt   | datetime2?      |                                                             |
| status       | tinyint          | ProviderStatus (Running/Success/Failed)                     |
| recordsAdded | int              | committed provider IOC rows; 0 on rollback                  |
| errorDetail  | nvarchar(max)?  | terminal error, including the first invalid record/reason   |

`INDEX (providerId, startedAt)`. There is no `recordsFailed`: an invalid record aborts the whole IOC
transaction. The history row is persisted separately with Failed status and `recordsAdded = 0`.

### `users`
| Column       | Type          | Notes                                     |
| ------------ | ------------- | ------------------------------------------ |
| id           | int PK        |                                            |
| username     | nvarchar(50)  | unique                                     |
| passwordHash | nvarchar(256) | PBKDF2/BCrypt hash                         |
| role         | tinyint       | UserRole (0 User / 1 Admin)                |
| createdAt    | datetime2     |                                            |

**Seed:** on first startup, if no Admin exists, create one from `.env` `ADMIN__USERNAME` / `ADMIN__PASSWORD`.

### `settings`  (key/value, runtime-applied)
| Column     | Type            | Notes                                          |
| ---------- | --------------- | ------------------------------------------------ |
| id         | int PK          |                                                  |
| key        | nvarchar(100)   | unique; `Logging:MinimumLevel`, `Worker:FetchConcurrency` (global); provider interval/enabled live on `providers` |
| value      | nvarchar(max)   |                                                  |
| updatedAt  | datetime2       |                                                  |

Worker/Api refresh settings via short-TTL cache (≤30 s). Changing `.env` connection/Rabbit/keys requires restart; **DB settings apply at runtime.**

| Layer in `.env` | Keys (secret, via dotenv at startup)                         |
| --------------- | ----------------------------------------------------------- |
| Database        | `DATABASE__CONNECTIONSTRING`                                |
| Messaging       | `RABBITMQ__URI` (or HOST/USER/PASS), `RABBITMQ__EXCHANGE`   |
| Providers       | `THREATFOX__APIKEY`                    |
| Auth            | `JWT__SECRET` (≥32 bytes), `JWT__ISSUER`, `JWT__AUDIENCE`, `JWT__EXPIRYMINUTES` (default 480) |
| Admin seed      | `ADMIN__USERNAME`, `ADMIN__PASSWORD`                        |

---

## 5. Normalization Specification (provider-scoped identity)

Applied on insert and user query input. It prevents duplicates for one provider while allowing the
same normalized value from different providers to remain independently attributed and grouped at query time.

### 5.1 URL
1. Trim whitespace. Must start with `http://` or `https://` (scheme preserved, lowercased). Reject otherwise.
2. Lowercase **scheme and DNS host**. When the host is an IP literal, apply the same strict IPv4/IPv6
   canonicalization as §§5.3–5.4 before rebuilding the URL. Do not convert IDNs to punycode; Unicode
   and punycode forms remain distinct.
3. Strip **default ports** (`:80` for http, `:443` for https) from `iocValue`, but retain the explicit port in `port`. Preserve non-default ports in both.
4. Strip **fragments** (`#...`).
5. If path is empty or `/`, drop the trailing slash → `https://example.com`.
6. **Keep path and query exactly as-is** (case, order). PhishTank URLs are unique per phish — query strings are significant.
7. Extract full lowercase `urlHost`, including subdomains, and calculate `parentDomain` as the registrable domain using Public Suffix List rules.
8. Length ≤ 2048 for the whole URL. Store the explicit port as an int constrained to 1–65535.

### 5.2 Domain
1. Trim whitespace; lowercase; strip one trailing `.` (root zone).
2. Do not convert IDNs to punycode; Unicode and punycode forms remain distinct.
3. Calculate `parentDomain` with Public Suffix List rules for every registrable DNS name. An apex
   such as `example.co.uk` stores itself; `api.example.co.uk` also stores `example.co.uk`.
4. Reject: contains scheme/`://`, path (`/`), port (`:`), whitespace, invalid label chars, TLD-only, or exceeds 253 chars / invalid label length (63).

### 5.3 IPv4
1. Parse strictly as IPv4 and store the canonical address; reject ambiguous forms such as leading-zero octets.
2. Incoming `ip:port` values store the canonical address in `iocValue` and the port separately.

### 5.4 IPv6
1. Parse strictly as IPv6 and store the compressed canonical representation.
2. `[2001:db8::1]:443` stores `2001:db8::1` in `iocValue` and `443` in `port`; bare IPv6 has NULL port.

### 5.5 Query type auto-detection (US-001)
`trim → strict bare or port-bearing IPv4/IPv6 parse? → IP type` else case-insensitive
`starts with http(s):// or contains :// → URL` else `valid domain → Domain` else **400 InvalidInput**
with a descriptive message. `1.2.3.4:443` and `[2001:db8::1]:443` search the normalized address and
port; a bare IP search groups all matching ports.

Textbook: `domain.com` is a Domain; `http://domain.com` is a **different** URL IOC whose
`urlHost = "domain.com"` → related, not the same row.

---

## 6. API Specification

Base path `/api`. All JSON. Errors return **ProblemDetails** (RFC 7807). List responses use an envelope:

```json
{ "items": [ ... ], "page": 1, "pageSize": 20, "totalCount": 123 }
```

Auth: `Authorization: Bearer <jwt>` (required except login).

### Public
| Method + Path                | Auth | Description                                                          |
| ---------------------------- | ---- | -------------------------------------------------------------------- |
| `POST /api/auth/login`       | none | Body `{username, password}` → `{token, username, role, expiresAt}`. Invalid creds → 401. |
| `GET /api/iocs/search?q=`    | any  | Auto-detect type (§5.5); normalize and exact-match; optional `tag` uses the same grouped source-attributed response. 400 on invalid input. |
| `GET /api/iocs/{id}`         | any  | Resolve the row's normalized type/value and return the full grouped IOC detail. 404 if missing. |
| `GET /api/iocs?type=&provider=&tag=&page=&pageSize=&orderBy=lastSeenUtc&dir=desc` | any | Browse grouped results; `type` ∈ ipv4/ipv6/domain/url; optional provider and normalized tag filters. |

**IOC detail response shape (`GET /api/iocs/{id}`):**
```json
{
  "iocValue": "https://evil.example/x?arg=1",
  "iocType": "url",
  "urlHost": "evil.example",
  "parentDomain": "evil.example",
  "firstSeenUtc": "2026-09-01T08:00:00Z",
  "lastSeenUtc": "2026-09-07T10:30:00Z",
  "sources": [
    {
      "provider": "ThreatFox",
      "providerRecordId": "12345",
      "providerDetailsLink": "https://threatfox.abuse.ch/ioc/12345/",
      "port": null,
      "firstSeenUtc": "2026-09-01T08:00:00Z",
      "lastSeenUtc": "2026-09-07T10:30:00Z",
      "threatType": "botnetCc",
      "malware": "Emotet",
      "threatFoxConfidenceLevel": 90,
      "tags": ["botnet"],
      "isFalsePositive": false
    },
    {
      "provider": "PhishTank",
      "providerRecordId": "67890",
      "providerDetailsLink": "https://phishtank.org/phish_detail.php?phish_id=67890",
      "port": null,
      "firstSeenUtc": "2026-09-02T08:00:00Z",
      "lastSeenUtc": "2026-09-02T09:00:00Z",
      "isVerified": true,
      "target": "Example Bank",
      "phishTankOnline": true,
      "phishTankDetails": [],
      "isFalsePositive": false
    }
  ],
  "related": {
    "urlHost": "evil.example",
    "parentDomain": "evil.example",
    "urlCount": 4,
    "urls": [
      { "iocValue": "https://evil.example/a", "iocType": "url" },
      { "iocValue": "https://evil.example/b", "iocType": "url" }
    ]
  }
}
```
Null provider-inapplicable properties are omitted, while valid `false`, `0`, and empty values remain.
Grouped `firstSeenUtc` and `lastSeenUtc` are respectively `MIN` and `MAX` across source rows. Sources
are not merged by provider: separate port-bearing rows may repeat the same provider. Related URLs are
resolved at query time through `urlHost` and `parentDomain`; no relation table is used. Tag filtering
selects groups having at least one matching `iocTags` row and leaves each returned tag under its
originating source entry.

### Admin (`/api/admin/*`, Role=Admin)
| Method + Path                                          | Description                                                        |
| ------------------------------------------------------ | ------------------------------------------------------------------ |
| `GET /api/admin/providers`                              | All providers + `{id, name, baseUrl, isEnabled, intervalMinutes, lastFetchStatus, lastFetchAt, nextFetchTime, fetchesPast24h, availableModes}`; budget and modes are derived from provider behavior/history, not extra provider columns. |
| `POST /api/admin/providers/{id}/fetch`                  | Manual trigger using the provider's default mode: ThreatFox → `recent`; PhishTank → `full`. Publishes `FetchJob(provider, Manual, mode)`; returns **202 Accepted** `{jobQueued:true}`. **409** if running, on cooldown, or PhishTank daily budget exhausted. |
| `POST /api/admin/providers/{id}/fetch/full`             | ThreatFox only; publishes `mode=full`. 400 if the adapter does not expose Full mode; **409** if already running or last fetch < 60 min cooldown. |
| `GET /api/admin/providers/{id}/history?page=&pageSize=` | `fetchHistory` for the provider.                                  |
| `GET /api/admin/summary`                                | Distinct grouped `{ total, ipv4, ipv6, domain, url }` counts.      |
| `GET /api/admin/logs?level=&count=`                     | Tail recent log entries `[{ timestamp, level, message, exception? }]`, `count ≤ 500`. |
| `GET /api/admin/settings`                               | Global settings + per-provider `enabled`/`interval` (masked `.env` secret values: `"*****"`). |
| `PUT /api/admin/settings`                               | Update global settings + per-provider enabled/interval (runtime). Validate → 400 on invalid; also `PUT /api/admin/settings/env` (masked write-back to `.env` for connection string / API keys — documented restart requirement). |

**Status codes:** 200 OK · 202 (fetch queued) · 400 invalid input / validation · 401 unauthenticated ·
403 forbidden (non-Admin on `/admin`) · 404 missing · 409 cooldown/daily-budget violated or concurrent fetch already running · 429 upstream rate-limited (surfaced as provider status, not HTTP).

**CORS:** dev `http://localhost:5173` (Vite); no auth-token exposure beyond JWT claims (username, role, exp, iss, aud).

---

## 7. Messaging (RabbitMQ)

```text
Exchange: tieto.fetches (topic, durable)
Queue:    tieto.fetch.jobs        (durable, routing key "fetch")
Retry:    tieto.fetch.jobs.retry  (delayed re-publish via TTL; or rabbitmq_delayed_message_exchange)
DLQ:      tieto.fetch.jobs.dlq    (terminal failures)
```

**Message contract (`FetchJob`):**
```json
{
  "jobId": "uuid",
  "providerId": 1,
  "trigger": "scheduled|manual",
  "mode": "recent|full",
  "attempt": 1
}
```

**Publishers:**
- **Api**: manual `POST /admin/providers/{id}/fetch[*]` (after passing guard checks).
- **Worker scheduler** (per provider scheduler loop): when `nextFetchTime` is due and enabled → publish `Scheduled` job, then set `nextFetchTime = now + intervalMinutes`.

**Trigger guards (checked by both Api triggers and the scheduler before publishing):**
- **Cooldown** — query the latest matching `fetchHistory.startedAt` by provider and mode: ThreatFox
  Recent must be at least 5 minutes old and Full at least 60 minutes old. Starting an attempt consumes
  the cooldown even when that attempt later fails; `providers.lastFetchAt` remains display-only.
- **Daily budget** — PhishTank fetches started in the rolling 24 h (`fetchHistory` rows) must be `< 3`. When exhausted: manual → 409; scheduled → `nextFetchTime` advanced until the oldest in-window fetch leaves the window. Retry attempts within one job do not consume extra budget.

**Consumer (Worker):**
- `prefetch = 1`; **per-provider run lock** (in-process + DB `lastFetchStatus = Running` guard) → no two fetches for the same provider overlap (redelivery or double-click safe).
- At the first job attempt: set `lastFetchStatus = Running` and persist
  `fetchHistory(trigger, mode, startedAt, status=Running)` outside the IOC transaction. Internal HTTP
  retries and RabbitMQ redeliveries reuse the provider's single Running history row under the
  per-provider lock; they do not insert another history row or consume another daily-budget slot.
- Adapter executes (ThreatFox recent/full, PhishTank full). **One DB transaction per fetch.** Batched inserts (≈2000/chunk via staged/EF `AddRange`+`SaveChanges` inside the outer transaction).
- **Type determination** for each incoming record per §5; resolve both `(providerId, iocType, iocValueHash, port)` and `(providerId, providerRecordId)`. Both keys must resolve to the same row when they already exist; conflicting rows indicate an invalid integration record and abort the batch. After a hash lookup, compare `iocValue` to detect a hash collision before treating records as equal.
- Upsert the resolved provider row or insert a new one. Preserve `createdAt` and the stored `firstSeenUtc`, refresh `updatedAt`, replace current metadata/tag associations, and replace `lastSeenUtc` with the provider's incoming value. No timestamp-regression guard is applied.
- **Any invalid record** → abort: rollback all IOC/tag changes, set `fetchHistory.status = Failed`, `recordsAdded = 0`, populate `errorDetail` with the offender, and set `providers.lastFetchStatus = Failed`. **No retry.**
- Transient exception (timeout, 5xx, socket): `nack` → retry exchange with delay → attempt++, re-publish
  while reusing the logical fetch's Running history row; after **attempt > 3** → **DLQ** → consumer
  marks that row `Failed` with the terminal error. `lastFetchStatus` transitions
  `Running → Success | Failed` throughout.
- **PhishTank 429 / rate-limit**: **no retry** → immediate `Failed` (quota guard).
- Success: commit, set `lastFetchStatus = Success`, `lastFetchAt = now`, `fetchHistory.status = Success`, persist `recordsAdded`, and set `nextFetchTime = now + intervalMinutes` for scheduled runs (unchanged for manual runs).

**Idempotency summary:** provider/value-hash/port and provider/provider-record unique indexes plus the
whole-batch transaction ensure redelivery cannot duplicate or partially update provider IOC rows.

---

## 8. Provider Adapters

| Adapter   | Endpoint(s)                                                                                  | Auth          | Parse                                                          |
| --------- | -------------------------------------------------------------------------------------------- | ------------- | -------------------------------------------------------------- |
| ThreatFox | recent: `https://threatfox-api.abuse.ch/v2/files/exports/{KEY}/domains_recent.csv` (+ urls, ip_port); full: `.../full.csv.zip` | `{KEY}` in URL | CSV/zip; typed ThreatFox metadata; IP and port stored separately |
| PhishTank | `https://data.phishtank.com/data/online-valid.json.gz` (with key if ever provided: insert app key path) | none (quota)   | JSON.gz; URL row with typed fields and complete validated `phishTankDetails` JSON |

- All HTTP via `IHttpClientFactory` + Polly (3 retries, exponential backoff) for **ephemeral** faults only; quota/429 routed to the no-retry path (§7).
- ThreatFox metadata is mapped into nullable typed `iocs` fields. PhishTank maps submission time to
  `firstSeenUtc`, verification time to `lastSeenUtc`, online/verified/target to typed fields, and all
  network detail objects to `phishTankDetails` JSON.
- Each adapter exposes result `{ added, error? }`; worker maps to transaction + history.

---

## 9. Worker Scheduler

- `BackgroundService` running in the **Worker** project.
- Loop (every ~15 s): read providers where `isEnabled AND nextFetchTime <= now` → apply trigger guards (§7) → publish scheduled `FetchJob`; if the budget window blocks the run, push `nextFetchTime` past the blocking window. No global timer lock between iterations required (run lock guards overlaps).
- On a failed scheduled run, scheduler **waits** for the next interval / manual trigger (PRD FR-008, FR-012) — no fast retry storm.
- `Worker:FetchConcurrency` limits simultaneous provider fetches.

---

## 10. Logging (Serilog)

- Sinks: **Console** (dev) + **rolling file** `logs/app-{date}.log` (retained 7 days), structured JSON.
- Minimum level runtime-editable via `settings["Logging:MinimumLevel"]` (reload hook).
- **Every fetch lifecycle event** logged: start (provider, mode, trigger), success (counts), transient retry, terminal failure (`errorDetail`), quota guard.
- `GET /api/admin/logs` tails the latest file: level filter, ≤500 rows, timestamp desc.

---

## 11. Frontend (React SPA)

**Pages / routes**

| Route          | Page           | Guard        |
| -------------- | -------------- | ------------ |
| `/login`       | Login          | public       |
| `/search`      | Search IOC + related host/URLs | User+ |
| `/iocs/:id`    | IOC detail     | User+        |
| `/browse`      | List + filters (type/provider), pagination | User+ |
| `/admin/providers` | Providers dashboard (status badges, last/next fetch, trigger buttons — recent/full for ThreatFox, full for PhishTank; PhishTank budget indicator "2/3 used today", cooldown countdown, history drill-down) | Admin |
| `/admin/summary`   | Database summary (total + per-type) | Admin |
| `/admin/logs`      | Log tail viewer | Admin       |
| `/admin/settings`  | Global + per-provider settings, masked `.env` edit | Admin |

**Patterns:** TanStack Query (server state/cache/invalidations), Zustand (auth/store: token, user, role),
Radix UI primitives, Zod schemas shared for input validation (mirrors backend §5/§6), react-router
guards on both route and request level (server re-enforces roles).

**Delivery:** MVP served dev-only (`vite dev` → proxy `/api` → api:8080); **final phase** ships a static build
served by **nginx** in docker-compose (`frontend` service).

---

## 12. Docker Compose (dev/MVP)

```yaml
services:
  mssql:     image mcr.microsoft.com/mssql/server:2022-latest   # port 1433, volume for data
  rabbitmq:  image rabbitmq:3.13-management                     # ports 5672, 15672, volume
  api:       build Api/, depends_on mssql rabbitmq, ports 8080 → 8080, env_file .env
  worker:    build Worker/, depends_on mssql rabbitmq, env_file .env
  # frontend: added in final phase (nginx serving dist, proxies /api → api)
```

- One `.env` at repo root drives the keys in §4.
- Healthchecks on api/worker (`/health`), wait-for-db + wait-for-rabbit at startup.
- Volumes: mssql data, rabbit data, api+worker logs.

---

## 13. Constraints & Known Limitations

- **PhishTank budget**: no app key → **3 fetches/day** (rolling 24 h, scheduled + manual combined), enforced before enqueue (manual 409, scheduler waits); a free key later may raise/remove it and enable ETag skip-checks (PRD OQ-5). 429 → immediate fail, no retry.
- **ThreatFox cooldowns**: recent ≥ 5 min, full ≥ 60 min; **ThreatFox expiry**: IOCs older than 6 months vanish from exports (2025-05-01 behavior) — additive-only means our DB keeps them longer than the source does; acceptable per PRD, documented.
- **ThreatFox cadence**: don't fetch recent more often than every 5 min, full more often than hourly (provider rule, enforced by interval guard).
- IPv4, IPv6, Domain, and URL are supported; URLs are capped at 2048 chars and pagination at `pageSize ≤ 100`.
- IDNs are not converted to punycode; Unicode and punycode representations remain separate identities.
- `parentDomain` accuracy depends on a current Public Suffix List dataset.
- **No refresh tokens**, no user self-registration; admin seed from `.env`.
- **`.env` edits need restart** for connection string / RabbitMQ URI / secret key material; DB settings apply immediately.

---

## 14. Testing Strategy (xUnit)

| Area | Tests |
| ---- | ----- |
| Normalization | §5 per type; IPv4/IPv6 canonicalization; URL casing/root slash/default port/fragment/path/query; no punycode conversion; invalid input rejection |
| Dedup | same provider/type/value-hash/port upserts; second provider remains a separate row; external provider IDs unique per provider |
| Additive update | re-fetch preserves `createdAt`, refreshes `updatedAt`, updates metadata/last-seen, and replaces current tags |
| Relation | full `urlHost` extraction; Public Suffix List `parentDomain`; exact-host and parent-domain URL lookups |
| Transaction | one bad record ⇒ all IOC/tag writes roll back; history remains Failed with zero records added and named error |
| Worker | retry cap 3 → DLQ; quota → immediate fail no retry; run-lock blocks concurrent same-provider |
| Auth | login ok/bad; role gate on admin endpoints (401/403) |
| Provider adapters | canned CSV/JSON fixtures; zip streaming; full ThreatFox metadata and PhishTank record/details mapping |

Fixtures: static recent CSV/JSON + PhishTank JSON inside test project; fake `IProviderClient` for
worker tests. README documents `dotnet test` + compose bring-up steps.

---

## 15. Open Questions (mirrors PRD §9)

- **Resolved** Cross-provider records are grouped at query time and returned as provider-attributed `sources`; null properties are omitted.
- **OQ-2** Fetch cancellation + `cancelled` status (adds cooperative cancel token during chunked writes).
- **Resolved** The .NET 10 solution and Clean Architecture projects exist at the repository root.
- **OQ-4** Full-dump scale limits & progress surface.
