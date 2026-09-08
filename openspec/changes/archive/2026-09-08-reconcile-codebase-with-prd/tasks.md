## 1. Replace the Domain Persistence Model

- [x] 1.1 Add the IOC type values and `Ioc`, `Tag`, and `IocTag` Domain entities using the existing PRD/Technical Specification field definitions, and verify `dotnet build ThreatIntelligence.slnx --no-restore` succeeds.
- [x] 1.2 Reshape `Provider` to use the common integer identity, ingestion settings, and navigation relationships, then verify focused entity tests cover required defaults and relationships.
- [x] 1.3 Delete the `ThreatFox` and `PhishTank` Domain entities and verify no production source references either removed entity type.

## 2. Replace the EF Core Schema

- [x] 2.1 Replace the raw-provider entity configurations with configurations for `Provider`, `Ioc`, `Tag`, and `IocTag`, including the specified table names, relationships, unique indexes, lookup indexes, lengths, and check constraints; verify EF model metadata tests assert the critical mappings.
- [x] 2.2 Replace the raw `DbSet` properties in `ThreatIntelDbContext` with common-model sets and verify a focused DbContext model test resolves all four sets and no raw provider entity.
- [x] 2.3 Generate a destructive EF migration that drops `providers.ThreatFox`, `providers.PhishTank`, and `threat.Provider`, creates the common tables, and seeds stable ThreatFox and PhishTank provider rows; verify `dotnet ef migrations script -p ThreatIntelligence.Infrastructure -s ThreatIntelligence.API` generates valid forward SQL.

## 3. Normalize Provider Records

- [x] 3.1 Implement one shared IOC normalization component for URL, domain, IPv4, IPv6, explicit ports, URL host/parent-domain values, and SHA-256 identity hashes according to the existing normalization specification; verify focused tests cover canonical values, rejected values, default and non-default ports, fragments, and hash stability.
- [x] 3.2 Change ThreatFox payload mapping to produce common provider-owned IOC values and normalized tags without constructing the removed raw entity, and verify fixture-based tests cover domain, URL, IP/port, metadata, and tag mapping.
- [x] 3.3 Change PhishTank payload mapping to produce common provider-owned URL IOC values while retaining complete validated details JSON, and verify fixture-based tests cover identity, metadata, details, and invalid records.

## 4. Persist Through the Common Model

- [x] 4.1 Implement a shared common-model upsert that resolves both provider-scoped identities, preserves creation time, refreshes current metadata and update time, and rejects conflicting identity matches; verify persistence tests cover insert, re-fetch update, cross-provider equivalence, and conflict failure.
- [x] 4.2 Replace IOC tag associations on successful upsert without deleting shared tag rows, and verify persistence tests cover tag normalization, replacement, and sharing between IOC rows.
- [x] 4.3 Update both provider services to persist their parsed datasets through the common upsert in one operation transaction with bounded tracking, and verify a failing record rolls back IOC and tag changes made by that operation.

## 5. Preserve and Verify Existing Operations

- [x] 5.1 Keep the three existing controller routes, MediatR commands, handlers, and typed response contracts wired to the updated services, and verify controller contract tests and focused handler tests pass.
- [x] 5.2 Remove obsolete raw-table configuration and persistence code, then verify repository search finds no production references to the removed entities or tables.
- [x] 5.3 Run `dotnet test ThreatIntelligence.slnx` and verify the complete solution test suite passes.
