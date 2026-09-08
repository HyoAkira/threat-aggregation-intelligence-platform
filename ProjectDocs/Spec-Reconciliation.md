# Spec Reconciliation — ProjectDocs vs OpenSpec

> Status: Cross-check record (documentation reconciled; implementation pending).
> Date: 2026-09-08

## 1. Relationship

`ProjectDocs/` (PRD + Technical-Specification) is the **target architecture** — the
full product as approved for planning. `openspec/` (specs + archived changes) captures **only
the first engineering scaffolding slice** actually implemented so far: per-provider raw
ingestion behind MediatR + EF Core + ProblemDetails. The planning documents are internally
reconciled; their target remains ahead of OpenSpec and executable code as detailed below.

## 2. Aligned (applied correctly)

- EF Core / MSSQL via `DefaultConnection`
- MediatR CQRS; controllers depend on `IMediator` only (no Infrastructure service injection)
- RFC 7807 `ProblemDetails` error handling (401/502/422/500 mapping)
- DotNetEnv loading of root `.env`
- Startup DB health check, fail-fast
- Dependency direction `Domain <- Application <- Infrastructure <- API`

## 3. Divergences / gaps

| Dimension                                    | PRD / Tech Spec (target)                                           | OpenSpec / code (implemented)                                                             | Status    |
| -------------------------------------------- | ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------- | --------- |
| Persistence model                            | Unified provider-owned `iocs`; provider/value/hash/port uniqueness; query-time grouping | Raw typed tables `providers.ThreatFox`, `providers.PhishTank`; provider-native uniqueness | Doc ahead |
| Public API                                   | `/api/auth`, `/api/iocs*`, `/api/admin*` (per Tech Spec §6)        | `/api/threatfox*`, `/api/phishtank*` (raw ingestion)                                      | Doc ahead |
| Enumerations                                 | `IocType` (incl. IPv6), `ProviderStatus`, `FetchTrigger`, `FetchMode`, `UserRole` | none (raw per-field data)                                                        | Doc ahead |
| Auth / Users                                 | FR-001, `users` table, admin seed                                  | absent                                                                                    | Not built |
| Search / normalization / dedup / attribution | FR-002/003/004/005                                                 | absent                                                                                    | Not built |
| Fetch lifecycle / history                    | FR-007, `fetchHistory`                                             | absent                                                                                    | Not built |
| Worker / RabbitMQ                            | §7, Worker project                                                 | absent                                                                                    | Not built |
| Settings / logs / summary                    | FR-010/011/012, admin API                                          | absent                                                                                    | Not built |
| Frontend                                     | §11 React SPA                                                      | absent                                                                                    | Not built |
| Target DB secret shape                       | full `DATABASE__CONNECTIONSTRING` in `.env`                        | `SA_PASSWORD` replaces the `__DB_PASSWORD__` placeholder in `appsettings.json`             | Target differs |

The `provider-schema` divergences (separate raw tables) and raw-ingestion endpoints are intentional
intermediate scaffolding. The target now uses one provider-owned `iocs` table, normalized `tags` /
`iocTags`, provider-scoped uniqueness, and application-level grouping; it is not yet implemented.

## 4. Resolved thread — `ProjectDocs/Untitled.md`

The notes about first-seen data, normalized tags, source-attributed verification, provider details,
and 180-day false-positive calculation have been incorporated into the PRD and Technical
Specification. MISP remains outside the current implementation scope.
