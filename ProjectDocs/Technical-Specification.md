# 🛠️ Technical Specification — Threat Intelligence Aggregator



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
| Frontend routing | TanStack Start/Router (role-guarded routes; selected implementation)                          |
| Backend          | .NET 10 Web API (Controller-based), Clean Architecture + CQRS (**MediatR**), FluentValidation |
| Persistence      | SQL Server 2025, EF Core (code-first), SQL Server via Docker                                  |
| Messaging        | MassTransit over RabbitMQ                                                                      |
| HTTP             | .NET `IHttpClientFactory`; job-level retries are owned by MassTransit                         |
| Logging          | Serilog (Console + rolling file; structured)                                                  |
| Auth             | JWT (HS256), ASP.NET Core Identity-free custom validator + `ClaimsPrincipal`                  |
| Conf/secrets     | dotenv package reading `.env`; ASP.NET configuration layered                                  |
| Tests            | xUnit, FluentAssertions, EF Core InMemory for the pure layers + a SQL-backed integration test |
| Infra            | Docker Compose: `mssql`, `rabbitmq`, `api`, `worker`, (`frontend`/nginx final phase)          |

Example NuGet set: `Microsoft.EntityFrameworkCore.SqlServer`, `MediatR`, `FluentValidation`,
`MassTransit.RabbitMQ`, `Serilog.AspNetCore`, `Serilog.Sinks.File`,
`Microsoft.IdentityModel.JsonWebTokens`, `DotNetEnv`.

---

## 3. Enumerations

```
IocType          : tinyint → 0 = Ipv4, 1 = Ipv6, 2 = Domain, 3 = Url
FetchTrigger     : tinyint → 0 = Scheduled, 1 = Manual
FetchMode        : tinyint → 0 = Recent, 1 = Full          (PhishTank only Full)
FetchStatus      : tinyint → 0 = Queued, 1 = Running, 2 = Success, 3 = Failed
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
| lastFetchStatus   | tinyint?       | FetchStatus summary of the latest fetch                 |
| lastFetchAt       | datetime2?     | last completed attempt                                  |
| nextFetchTime     | datetime2?     | NULL until the first successful manual fetch; then scheduled by the worker |

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
| jobId        | uniqueidentifier | unique logical job ID returned by the trigger endpoint      |
| providerId   | int FK           | required → providers                                        |
| trigger      | tinyint          | FetchTrigger (Scheduled/Manual)                             |
| mode         | tinyint          | FetchMode (Recent/Full)                                     |
| startedAt    | datetime2       |                                                             |
| finishedAt   | datetime2?      |                                                             |
| status       | tinyint          | FetchStatus (Queued/Running/Success/Failed)                 |
| recordsAdded | int              | newly inserted IOC rows committed by the fetch; 0 on rollback |
| recordsUpdated | int            | existing IOC rows updated by the fetch; 0 on rollback       |
| errorDetail  | nvarchar(max)?  | terminal error, including the first invalid record/reason   |

`INDEX (providerId, startedAt)`. There is no `recordsFailed`: an invalid record aborts the whole IOC
transaction. The history row is persisted separately with Failed status and both counters set to 0.
An upsert counts as updated when it targets an existing IOC row, even if the incoming values are
identical; therefore `recordsAdded + recordsUpdated` is the number of provider records committed.
`startedAt` is set when the job is accepted, so displayed duration includes queue wait time.

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
| key        | nvarchar(100)   | unique; `Logging:MinimumLevel`; provider interval/enabled live on `providers` |
| value      | nvarchar(max)   |                                                  |
| updatedAt  | datetime2       |                                                  |

Worker/Api refresh settings via short-TTL cache (≤30 s). Changing `.env` connection/Rabbit/keys requires restart; **DB settings apply at runtime.**

Provider seeds are deterministic: ID 1 is ThreatFox (`https://threatfox-api.abuse.ch`), enabled with
a 5-minute interval; ID 2 is PhishTank (`https://data.phishtank.com`), enabled with an 8-hour
interval. Both begin with `nextFetchTime = NULL` and wait for a successful manual fetch. Application
time is UTC and is obtained through .NET `TimeProvider` so tests can control it.

| Layer in `.env` | Keys (secret, via dotenv at startup)                         |
| --------------- | ----------------------------------------------------------- |
| Database        | `DATABASE__CONNECTIONSTRING`                                |
| Messaging       | `RABBITMQ__URI` (or HOST/USER/PASS), `RABBITMQ__EXCHANGE`   |
| Providers       | `THREATFOX__APIKEY`                    |
| Auth            | `JWT__SECRET` (≥32 bytes), `JWT__ISSUER`, `JWT__AUDIENCE`; token lifetime is fixed at 8 hours for MVP |
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

Auth: `Authorization: Bearer <jwt>` (required for business endpoints except login; `/health` and `/health/live` remain anonymous).

### Public
| Method + Path                | Auth | Description                                                          |
| ---------------------------- | ---- | -------------------------------------------------------------------- |
| `POST /api/auth/login`       | none | Body `{username, password}` → `{token, username, role, expiresAt}`. Invalid creds → 401. |
| `GET /api/iocs/search?q=&tag=&provider=` | any | `q` required; auto-detect/normalize and exact-match. Optional tag/provider filters narrow sources. Invalid input → 400; valid absent match → 404. |
| `GET /api/iocs/{id}`         | any  | Resolve the row's normalized type/value and return the full grouped IOC detail without inline relations. 404 if missing. |
| `GET /api/iocs/{id}/relations?relation=urlHost|parentDomain&page=&pageSize=` | any | Paginated related groups; default page 1/pageSize 20, maximum 100. IP groups → 400. |
| `GET /api/iocs?type=&provider=&tag=&page=&pageSize=&orderBy=lastSeenUtc&dir=desc` | any | Browse grouped results; filters compose, pageSize ≤ 100, NULL aggregates last, stable type/value tie-break. |

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
  ]
}
```
Null provider-inapplicable properties are omitted, while valid `false`, `0`, and empty values remain.
Grouped `firstSeenUtc` and `lastSeenUtc` are respectively `MIN` and `MAX` across source rows. Sources
are not merged by provider: separate port-bearing rows may repeat the same provider. Relations are
resolved on demand through the paginated relations endpoint using stored `urlHost` and `parentDomain`;
no relation table is used. Exact-search tag/provider filters narrow returned sources, while browse tag
filtering selects matching groups and keeps each tag under its originating source entry.

### Admin (`/api/admin/*`, Role=Admin)
| Method + Path                                          | Description                                                        |
| ------------------------------------------------------ | ------------------------------------------------------------------ |
| `GET /api/admin/providers`                              | All providers + `{id, name, baseUrl, isEnabled, intervalMinutes, lastFetchStatus, lastFetchAt, nextFetchTime, availableModes}`. |
| `POST /api/admin/providers/{id}/fetch`                  | Manual trigger using the provider's default mode: ThreatFox → `recent`; PhishTank → `full`. Creates Queued history, publishes `FetchJob`, then returns **202 Accepted** `{jobId,status:"queued"}` after broker confirmation. **409** if running or on cooldown. |
| `POST /api/admin/providers/{id}/fetch/full`             | ThreatFox only; publishes `mode=full`. 400 if the adapter does not expose Full mode; **409** if already running or last fetch < 60 min cooldown. |
| `GET /api/admin/providers/{id}/history?page=&pageSize=` | `fetchHistory` for the provider.                                  |
| `GET /api/admin/fetch-history/{jobId}`                  | Current job status, timing, added/updated counts, and terminal error. |
| `GET /api/admin/summary`                                | Distinct grouped `{ total, ipv4, ipv6, domain, url }` counts.      |
| `GET /api/admin/logs?level=&count=`                     | Tail newest safe log entries `[{ timestamp, level, message, exception? }]`; default count 100, maximum 500; redact credentials, tokens, keys, and credential-bearing URLs. |
| `GET /api/admin/settings`                               | Global settings + per-provider `enabled`/`interval`. |
| `PUT /api/admin/settings`                               | Atomically update log level + provider enabled/interval at runtime. ThreatFox scheduled interval ≥ 5 min; PhishTank ≥ 480 min. Any invalid value → 400 with no writes. |

**Status codes:** 200 OK · 202 (fetch queued) · 400 invalid input / validation · 401 unauthenticated ·
403 forbidden (non-Admin on `/admin`) · 404 missing · 409 cooldown violated or concurrent fetch already running · 429 upstream rate-limited (surfaced as provider status, not HTTP).

**CORS:** dev `http://localhost:5173` (Vite); no auth-token exposure beyond JWT claims (username, role, exp, iss, aud).

---

## 7. Messaging (RabbitMQ)

```text
Exchange: tieto.fetches (topic, durable)
Queue:    tieto.fetch.jobs        (durable, routing key "fetch")
Error:    tieto.fetch.jobs_error  (MassTransit default error queue after retry exhaustion)
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
- Publishers create one Queued `fetchHistory` row and publish directly with MassTransit publisher
  confirmation. No transactional outbox is used in the MVP. A failed publish marks that history row
  Failed; the small crash window between the database write and broker confirmation is accepted.

**Trigger guards (checked by both Api triggers and the scheduler before publishing):**
- **Cooldown** — query the latest matching `fetchHistory.startedAt` by provider and mode: ThreatFox
  Recent must be at least 5 minutes old and Full at least 60 minutes old. Starting an attempt consumes
  the cooldown even when that attempt later fails; `providers.lastFetchAt` remains display-only.

**Consumer (Worker):**
- `prefetch = 1` and consumer concurrency `1`; the MVP processes only one fetch globally at a time.
  The consumer rechecks the persisted job/provider state and skips obsolete duplicate messages.
- The publisher creates `fetchHistory(trigger, mode, startedAt, status=Queued)`. At the first job
  attempt, the Worker changes that row and `providers.lastFetchStatus` to Running. Internal HTTP
  calls are not retried independently. MassTransit retries reuse the provider's single Running history row under the
  same `jobId`; they do not insert another history row.
- Adapter executes (ThreatFox recent/full, PhishTank full). **One DB transaction per fetch.** Process
  approximately 2,000 provider records per chunk using EF `AddRange`/`SaveChanges` inside the outer
  transaction, then clear the change tracker before the next chunk.
- At fetch start, load normalized tag names and IDs with `AsNoTracking` into a scalar
  `Dictionary<string,int>`. For each chunk, normalize and deduplicate its tag names, insert each name
  missing from the dictionary once, save to obtain its ID, and update the dictionary. IOC-tag rows
  reference scalar IDs rather than tracked `Tag` entities, so clearing the change tracker cannot
  cause later chunks to recreate tags. The unique index on `tags.name` remains the database safeguard.
  Because every chunk shares the outer transaction, any later failure also rolls back tags inserted
  by earlier chunks.
- **Type determination** for each incoming record per §5; resolve both `(providerId, iocType, iocValueHash, port)` and `(providerId, providerRecordId)`. The provider record ID is authoritative and may update that row's normalized IOC identity. If the new normalized identity already belongs to another row for the provider, reject the entire fetch rather than merge rows. After a hash lookup, compare `iocValue` to detect a hash collision before treating records as equal.
- Upsert the resolved provider row or insert a new one. Preserve `createdAt` and the stored `firstSeenUtc`, refresh `updatedAt`, replace current metadata/tag associations, and replace `lastSeenUtc` with the provider's incoming value. No timestamp-regression guard is applied.
- **Any invalid record** → abort: rollback all IOC/tag changes, set `fetchHistory.status = Failed`, `recordsAdded = 0`, `recordsUpdated = 0`, populate `errorDetail` with the offender, and set `providers.lastFetchStatus = Failed`. **No retry.**
- Transient exception (timeout, 5xx, socket, temporary database failure): MassTransit retries after
  10 seconds, 30 seconds, and 1 minute while reusing the logical fetch's Running history row. After
  the initial execution and three retries fail, MassTransit moves the message to the default `_error`
  queue and the history row is marked `Failed`. `lastFetchStatus` transitions
  `Running → Success | Failed` throughout.
- **PhishTank 429 / rate-limit**: **no retry** → immediate `Failed` (quota guard).
- Success: commit, set `lastFetchStatus = Success`, `lastFetchAt = now`, `fetchHistory.status = Success`, and persist `recordsAdded` and `recordsUpdated`. The first successful manual fetch initializes `nextFetchTime = now + intervalMinutes`; subsequent scheduled publications advance it by the configured interval.

**Idempotency summary:** provider/value-hash/port and provider/provider-record unique indexes plus the
whole-batch transaction ensure redelivery cannot duplicate or partially update provider IOC rows.

---

## 8. Provider Adapters

| Adapter   | Endpoint(s)                                                                                  | Auth          | Parse                                                          |
| --------- | -------------------------------------------------------------------------------------------- | ------------- | -------------------------------------------------------------- |
| ThreatFox | recent: `https://threatfox-api.abuse.ch/v2/files/exports/{KEY}/domains_recent.csv` (+ urls, ip_port); full: `.../full.csv.zip` | `{KEY}` in URL | CSV/zip; typed ThreatFox metadata; IP and port stored separately |
| PhishTank | `https://data.phishtank.com/data/online-valid.json.gz` (with key if ever provided: insert app key path) | none (quota)   | JSON.gz; URL row with typed fields and complete validated `phishTankDetails` JSON |

- All HTTP uses `IHttpClientFactory` with an explicit timeout. Adapters do not perform their own
  retries; transient failures are retried at the job level by MassTransit (§7).
- ThreatFox metadata is mapped into nullable typed `iocs` fields. PhishTank maps submission time to
  `firstSeenUtc`, verification time to `lastSeenUtc`, online/verified/target to typed fields, and all
  network detail objects to `phishTankDetails` JSON.
- Each adapter streams normalized provider records or raises a typed failure. The shared atomic pipeline returns `{ recordsAdded, recordsUpdated }`; the worker maps that result to fetch history.

---

## 9. Worker Scheduler

- `BackgroundService` running in the **Worker** project.
- Loop (every ~15 s): read providers where `isEnabled AND nextFetchTime IS NOT NULL AND nextFetchTime <= now` → apply trigger guards (§7) → publish scheduled `FetchJob`. A newly seeded provider waits for its first successful manual fetch before scheduling begins.
- After an interval update, recompute an already-run provider's `nextFetchTime` from its last successful completion plus the new interval. A past result is due on the next tick; a never-run provider remains NULL. Disabling prevents publication and re-enabling uses the recomputed time.
- On a failed scheduled run, scheduler **waits** for the next interval / manual trigger (PRD FR-008, FR-012) — no fast retry storm.
- MVP consumer concurrency is fixed at one. Configurable parallel fetching is deferred until needed.

---

## 10. Logging (Serilog)

- Sinks: **Console** (dev) + **rolling JSON files** `logs/api-{date}.json` and `logs/worker-{date}.json`
  (retained 7 days). API request logs accept or generate `X-Correlation-ID`, return it on the response,
  and include it in structured request events. Secrets are never written to logs.
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
| `/admin/providers` | Providers dashboard (status badges, last/next fetch, trigger buttons — recent/full for ThreatFox, full for PhishTank; interval and cooldown information, history drill-down) | Admin |
| `/admin/summary`   | Database summary (total + per-type) | Admin |
| `/admin/logs`      | Log tail viewer | Admin       |
| `/admin/settings`  | Global + per-provider runtime settings | Admin |

**Patterns:** TanStack Query (server state/cache/invalidations), Zustand (auth/store: token, user, role) backed by per-tab `sessionStorage`,
Radix UI primitives, Zod schemas shared for input validation (mirrors backend §5/§6), react-router
guards on both route and request level (server re-enforces roles). TanStack Start/Router is the selected
Vite-based routing implementation for this repository; it replaces the originally proposed `react-router`
choice while preserving the same role-guard requirement.

**Delivery:** MVP served dev-only (`vite dev` → proxy `/api` → api:8080); **final phase** ships a static build
served by **nginx** in docker-compose (`frontend` service).

---

## 12. Docker Compose (dev/MVP)

```yaml
services:
  mssql:     image mcr.microsoft.com/mssql/server:2025-latest   # port 1433, volume for data
  rabbitmq:  image rabbitmq:3.13-management                     # ports 5672, 15672, volume
  api:       build src/ThreatIntelligence.API/Dockerfile, depends_on mssql rabbitmq, ports 8080 → 8080, env_file .env
  worker:    build src/ThreatIntelligence.Worker/Dockerfile, depends_on mssql rabbitmq, ports 8081 → 8080, env_file .env
  # frontend: added in final phase (nginx serving dist, proxies /api → api)
```

- One `.env` at repo root drives the keys in §4.
- Healthchecks on api/worker (`/health`), plus liveness endpoints (`/health/live`); Compose waits for
  healthy MSSQL and RabbitMQ before starting either host.
- Volumes: mssql data, rabbit data, API logs, and Worker logs. The test SQL Server is profile-gated
  (`test`) and uses host port 1432.

---

## 13. Constraints & Known Limitations

- **PhishTank cadence**: full dump is scheduled every 8 hours. 429 → immediate fail, no retry.
- **ThreatFox cooldowns**: recent ≥ 5 min, full ≥ 60 min; **ThreatFox expiry**: IOCs older than 6 months vanish from exports (2025-05-01 behavior) — additive-only means our DB keeps them longer than the source does; acceptable per PRD, documented.
- **ThreatFox cadence**: don't fetch recent more often than every 5 min, full more often than hourly (provider rule, enforced by interval guard).
- IPv4, IPv6, Domain, and URL are supported; URLs are capped at 2048 chars and pagination at `pageSize ≤ 100`.
- IDNs are not converted to punycode; Unicode and punycode representations remain separate identities.
- `parentDomain` accuracy depends on a current Public Suffix List dataset.
- **No refresh tokens**, no user self-registration; admin seed from `.env`.
- Operators edit `.env` manually; the application never reads secrets back through an endpoint or
  writes the file. Connection, RabbitMQ, API-key, JWT, and seed changes require restart.

---

## 14. Testing Strategy (xUnit)

| Area | Tests |
| ---- | ----- |
| Normalization | §5 per type; IPv4/IPv6 canonicalization; URL casing/root slash/default port/fragment/path/query; no punycode conversion; invalid input rejection |
| Dedup | same provider/type/value-hash/port upserts; second provider remains a separate row; external provider IDs unique per provider |
| Additive update | re-fetch preserves `createdAt`, refreshes `updatedAt`, updates metadata/last-seen, and replaces current tags |
| Relation | full `urlHost` extraction; Public Suffix List `parentDomain`; exact-host and parent-domain URL lookups |
| Transaction | one bad record ⇒ all IOC/tag writes roll back; history remains Failed with zero added/updated records and named error |
| Worker | one-at-a-time processing; three MassTransit retries → `_error`; quota → immediate fail no retry |
| Auth | login ok/bad; role gate on admin endpoints (401/403) |
| Provider adapters | canned CSV/JSON fixtures; zip streaming; full ThreatFox metadata and PhishTank record/details mapping |

Fixtures: static recent CSV/JSON + PhishTank JSON inside test project; fake `IProviderClient` for
worker tests. README documents `dotnet test` + compose bring-up steps.

Frontend foundation tests use Jest with `ts-jest`; the repository includes a TypeScript schema smoke
test. Frontend verification also runs linting and the Vite production build. The complete .NET solution
restore, build, and test suite has been verified in a clean .NET 10 container.

---

## 15. Open Questions (mirrors PRD §9)

- **Resolved** Cross-provider records are grouped at query time and returned as provider-attributed `sources`; null properties are omitted.
