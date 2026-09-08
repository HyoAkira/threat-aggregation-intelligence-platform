# 📋 PRD — Threat Intelligence Aggregator

> **Status:** Approved for development planning
> **Source doc (superseded for decisions):** `04 - Archive/PRE-DEVELOPMENT — Project Kickoff Paper.md`
> **Location of this PRD:** `ThreatIntelligence/ProjectDocs/PRD.md`

---

## 1. Overview

A standalone web application that collects threat indicators (IOCs) from **public** cyber
threat intelligence sources, normalizes them into one common provider-record model, prevents
duplicates within each provider, groups equivalent records across providers at query time, and
exposes a searchable web interface for Users plus an operations console for Admins.

**Flow:** `Collect → Normalize → Deduplicate → Store → Search → Trace Sources`

---

## 2. Objectives

- Consolidate indicators from **multiple public providers** into one provider-attributed dataset.
- Preserve provider-specific metadata while grouping equivalent IOC records in search results.
- Provide **exact-match search** for IPv4, IPv6, domain, and URL indicators.
- Keep data **additive-only** (no destructive deletes) while supporting re-fetch/update.
- Remain **self-contained** (no internal enterprise systems, no proprietary feeds).

### Success Metrics

| Metric                                       | Target                       |
| -------------------------------------------- | ---------------------------- |
| Integrated public sources                    | ≥ 2 (ThreatFox, PhishTank)   |
| Supported indicator types                    | IPv4, IPv6, Domain, URL      |
| Records normalized to common model           | 100% of accepted records     |
| Duplicate provider IOC records               | 0 (enforced by unique indexes) |
| IOCs with source attribution                 | 100%                         |
| Data update via re-fetch                     | Supported                    |
| Critical business rules under automated test | 100%                         |

---

## 3. In Scope / Out of Scope

**[In scope]** Multi-source collection (recent + full), normalization, provider-scoped dedup,
query-time source grouping, additive re-fetch/update, exact-match and tag search + filtered listing, JWT auth (User/Admin),
provider status monitoring, fetch history, database summary, system logs tailing,
runtime-editable settings, and automated tests for core rules.

**[Out of scope]** Real-time/monitoring integration, DPI/network capture, endpoint monitoring,
malware analysis, SIEM/SOAR, automated response/blocking, mobile app, building a custom source,
user-generated feeds, machine learning/threat scoring, and **fuzzy/full-text search**
(deferred).

**[Non-goals]** Not an enterprise TIP, no threat *detection* (search known indicators only), no
dependence on internal APIs.

---

## 4. Users & Roles

| Role   | Capabilities                                                        |
| ------ | ------------------------------------------------------------------- |
| User   | Login; search IOCs; view grouped IOC details + related hosts/parent domains/URLs; browse/filter list. |
| Admin  | All User capabilities + provider dashboard, fetch history, trigger fetches, system logs, database summary, settings. |

Auth is **JWT** (HS256, no refresh tokens in MVP). Login required for everything; `/admin` areas are Admin-only. First admin user is **seeded from `.env`** on first startup.

---

## 5. Data Sources & Ingestion Model

| Provider  | Scheduled (interval, admin-changeable) | Manual trigger (UI)                                                                  | Data available                              |
| --------- | -------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------- |
| ThreatFox | **Recent additions** (last 48 h)       | **Recent** and **Full dump** (two buttons, manual)                                   | Domains, URLs, IPs (`ip:port`)              |
| PhishTank | **Full dump** (`online-valid.json`)    | **Full dump** (button, manual)                                                       | URLs (+ per-entry `target`, detail URL, IP) |

- **PhishTank has no "recent" endpoint** — it is always a full-dump fetch.
- **Manual triggers are available for every provider/mode from the UI**: ThreatFox *recent*, ThreatFox *full*, and PhishTank *full*.
- Scheduled jobs respect cadence (ThreatFox recent by interval; PhishTank by interval **within its daily budget**).
- Provider **intervals are runtime-editable** (DB settings, applied without restart).
- Ingestion is **additive-only**: new provider IOC rows are added, existing rows are upserted,
  and records absent from later feeds are retained.
- A provider IOC row is calculated as `isFalsePositive = true` when its `updatedAt` is older than
  exactly 180 days. A successful reappearance refreshes `updatedAt` and clears that calculated state.

### Constraints inherited from providers

- **ThreatFox** requires an Auth-Key (free) included in export URLs; IOCs older than 6 months are
  expired/removed from exports; full dump regenerates hourly, recent every 5 min (don't fetch more often).
  Guards: recent fetches **≥ 5 min apart**; full fetches **≥ 60 min apart** (cooldown, enforced at trigger time).
- **PhishTank** (no app key): fetches are capped at a **3-per-day budget**, covering scheduled
  **and** manual combined. Both triggers are denied when the rolling-24 h budget is exhausted; files
  update hourly. If a free app key is registered later, the budget can be relaxed (see **OQ-5**).

---

## 6. Functional Requirements

### FR-001 — Authentication & Authorization (JWT)
- `POST /api/auth/login` issues a JWT; HS256; 8 h lifetime; no refresh token (MVP).
- First-run admin seeded from `.env` (`ADMIN__USERNAME`, `ADMIN__PASSWORD`).
- Client routes and all API endpoints enforce roles (User / Admin).

### FR-002 — Search an IOC
- Search accepts IPv4, IPv6, domain, or URL. Input **auto-classified**; invalid input rejected (400) with a clear message; "not found" clearly indicated.
- Exact match uses the normalized value. Rows with the same normalized type/value are returned as
  one grouped result with a `sources` collection; multiple rows from one provider may remain as
  separate source entries when ports differ.
- Tag search returns the same grouped shape and identifies which source supplied each matched tag.
- URL results expose their full `urlHost` and registrable `parentDomain` for host and parent-domain searches.

### FR-003 — IOC details & source tracing
- Detail results show IOC value, type, query-time global first/last seen (`MIN`/`MAX` across grouped
  rows), **all source entries**, and provider-specific fields under the source that supplied them.
- Null provider-inapplicable properties are omitted from JSON; valid `false`, `0`, and empty values
  are retained. PhishTank `details` remains complete validated JSON.
- URL details include related exact hosts, registrable parent domains, and URLs.

### FR-004 — Deduplication
- Each database row belongs to exactly one provider. Provider-scoped identity is
  `(ProviderId, IocType, IocValueHash, Port)`, where `IocValueHash` is the persisted SHA-256 hash of
  the normalized value. The same normalized IOC from different providers is stored in separate rows
  and grouped by the application.
- `(ProviderId, ProviderRecordId)` is also unique. Re-fetch upserts the existing provider row.

### FR-005 — Normalization
- All incoming values are normalized before provider-scoped identity checks and query matching; rules
  are in Technical Specification §5.
- A URL and its bare host domain remain different IOC types. URL paths and queries are preserved,
  fragments are removed, and only a root trailing slash is removed.
- Ports are stored separately (`1-65535`) and participate in provider-row uniqueness. `urlHost`
  retains the full subdomain; `parentDomain` contains the registrable domain using Public Suffix List rules.

### FR-006 — Additive update / re-sync
- Re-fetch adds or upserts provider IOC rows, preserves `createdAt`, refreshes `updatedAt`, replaces
  current provider metadata and tags, and updates `lastSeenUtc`; DB failures leave **no partial IOC data**.

### FR-007 — Fetch lifecycle, statuses & history
- Provider live status is one of **`running` / `success` / `failed`**, updated continuously during a fetch.
- Every run is recorded in `fetchHistory` (trigger, mode, started/finished, status, records added, error detail).
- Scheduler publishes due jobs; manual triggers publish immediately; both go through RabbitMQ.
- **Trigger guards**: provider cooldowns (§5) and PhishTank's 3-per-day budget are enforced **before** a fetch is enqueued (denied → HTTP 409 / skipped scheduler tick).

### FR-008 — Retry policy (RabbitMQ)
- **Transient errors** (network, 5xx, timeout): retry up to **3 attempts** with delay, then dead-letter → status `failed`.
- **PhishTank quota/rate-limit (429)**: **no retry**, immediate `failed` (protects the 3-per-day budget).
- **PhishTank daily budget (3/day)** and provider cooldowns are enforced **before** a fetch is enqueued (trigger → 409); the budget covers scheduled + manual combined.
- **Invalid-record abort**: **no retry** — deterministic; `failed` with the offending record + reason in the error detail.

### FR-009 — Whole-batch transaction on failure
- **Any single invalid record fails the entire fetch.** The IOC batch rolls back (nothing written),
  while its separate history row remains with status `failed`, `recordsAdded = 0`, and an error that
  explicitly names the first failing record and reason.

### FR-010 — Settings
- **Per-provider (DB, runtime):** enabled, interval (minutes).
- **Global (DB, runtime):** log level, fetch concurrency.
- **`.env` (dotenv, source of truth, secret):** DB connection string, provider API keys, RabbitMQ URI + credentials, JWT secret, admin seed credentials.
- Admin UI can edit `.env` values (masked display, validated write-back; applied on restart for wiring-infra values). DB settings apply immediately.

### FR-011 — Logs
- Serilog → console + rolling file; admin `GET /api/admin/logs` tails recent entries (level filter, N entries, timestamps). Failed fetches are always logged.

### FR-012 — Database summary
- Admin sees distinct grouped IOC counts in total and per type (IPv4 / IPv6 / Domain / URL), consistent with search grouping.

---

## 7. User Stories & Acceptance Criteria

| Story | As | I want to | AC summary |
| ----- | -- | --------- | ---------- |
| US-001 | User | search an IOC (IPv4/IPv6/domain/URL) | exact-match search; clear not-found state; invalid input rejected |
| US-002 | User | view IOC details | grouped value/type, aggregate timestamps, provider-attributed sources, related host/parent-domain/URLs |
| US-003 | User | see which providers reported an IOC | provider rows grouped for display; every field and tag remains attributed to its source |
| US-004 | Admin | monitor provider status | running/success/failed badge, last fetch time, error visibility |
| US-005 | Admin | view DB summary | total + per-type counts |
| US-006 | Admin | view system logs | timestamped, filterable tail; failed fetches present |
| US-007 | Admin | manage settings | view + update; invalid rejected; runtime-applied |
| US-008 | Admin | collect data | scheduled + manual triggers for every provider/mode; status logged; cooldowns/budget enforced; failures don't crash the app |
| US-009 | System | normalize from different providers | common provider-row model; correct type; typed ThreatFox fields and PhishTank details preserved |
| US-010 | System | prevent duplicates | unique provider/value/port and provider/external-id indexes; cross-provider rows grouped at query time |
| US-011 | Admin | re-fetch / update data | atomic upsert; current metadata/tags replaced; timestamps refreshed; no provider-scoped duplicates |
| US-012 | Admin | learn about provider failures | no crash; logged; status=failed; app usable when one provider down |

---

## 8. Non-Functional Requirements

- **Fault tolerance:** one failing provider never blocks searches or other providers.
- **Performance:** bulk-fetch inserts batched (≈2000 rows/chunk) inside one transaction; exact-match + list queries index-backed; PhishTank/ThreatFox scale documents in Technical Specification.
- **Security:** password hashing (own mechanism in backend, e.g. PBKDF2/BCrypt), JWT with role claims, admin endpoints server-enforced, secrets only in `.env`, never in DB or frontend.
- **Reliability:** durable RabbitMQ queue, `prefetch=1`, per-provider run lock, idempotent upserts.
- **Deployability:** Docker Compose (`mssql`, `rabbitmq`, `api`, `worker`; `nginx+frontend` in final phase); one `.env`.

---

## 9. Open Questions (carried; must be resolved before or during development)

**Resolved — Cross-provider presentation.** Equivalent provider rows are grouped at query time. The
response contains a `sources` collection, provider-inapplicable null properties are omitted, and no
provider assertions are flattened into an unattributed shared payload.

**OQ-2 — Fetch cancellation.** Statuses are limited to `running / success / failed` until a
cancellation requirement is agreed; if Admin needs to cancel a long full-dump fetch, a `cancelled`
state + worker cooperation (check-for-cancel token during chunk writes) must be added.

**Resolved — Project scaffolding.** The .NET 10 solution and Clean Architecture project structure
exist in the current repository; production projects remain at the repository root.

**OQ-4 — ThreatFox full-dump scale.** Confirm acceptable ingest time/limits for an MVP full dump
(streamed, batched); decide whether `fetchHistory` should expose progress, and whether the manual full
fetch should require an Admin confirmation in the UI.

**OQ-5 — PhishTank app key (budget relaxation).** For now the PhishTank fetch budget is fixed at
**3/day** (no app key). If a free app key is registered later: budget can be raised/removed, hourly
updates become safe, and unlimited `ETag` HEAD checks can skip downloads when the dump hasn't changed.
Keep the budget guard in place either way; make it a configurable setting when the key is added.
