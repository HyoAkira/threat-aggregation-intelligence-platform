## Context

See `proposal.md` for motivation. The current Domain and DbContext expose `Provider`, `ThreatFox`, and `PhishTank`; each provider service parses directly into its raw entity and executes a provider-specific SQL `MERGE` once per record. The existing controller-to-MediatR-to-service flow is already established and does not need a routing or application-layer redesign.

The database contains development data only and may be replaced destructively. The common field definitions, IOC types, normalization rules, physical naming, indexes, and constraints are already specified in `ProjectDocs/Technical-Specification.md` sections 3-5 and are not redesigned here.

## Goals / Non-Goals

**Goals:**

- Give both existing provider services one provider-attributed IOC persistence model.
- Enforce provider-scoped IOC identity and normalized tag associations in the database.
- Keep the current endpoint, MediatR command, handler, and typed response boundaries stable.
- Make the schema replacement explicit and reproducible through an EF Core migration.

**Non-Goals:**

- Preserve or convert rows from the provider-specific development tables.
- Add tables or behavior not needed by the current ingestion operations.
- Change product documentation, provider fetch scheduling, or the public endpoint surface.

## Decisions

### Replace raw entities instead of wrapping them

Delete the `ThreatFox` and `PhishTank` Domain entities and their EF configurations. Add `Ioc`, `Tag`, and `IocTag`, and reshape `Provider` to the common schema. This prevents two persistence models from coexisting and avoids synchronization logic between raw and common rows.

Alternative: retain raw tables as staging tables and project into `iocs`. Rejected because the current synchronous operations do not need durable staging, and retaining both models adds duplicate storage and failure modes.

### Limit the schema to current ingestion needs

Create only `providers`, `iocs`, `tags`, and `iocTags`. Do not add unused tables in anticipation of later features. Provider rows for ThreatFox and PhishTank use stable seeded integer identities so the existing services can resolve ownership without hard-coded database assumptions.

Alternative: create every table in the target schema now. Rejected because users, settings, and fetch lifecycle have no current code path and belong with the features that use them.

### Normalize before persistence through shared application logic

Provider adapters continue to parse their native payloads, then pass candidate values through one reusable normalization component implementing the existing normalization specification. The normalized value, type, explicit port, URL host/parent domain where applicable, and SHA-256 hash form the persistence input. Invalid records fail the current operation rather than being stored with empty or generated fallback values.

Alternative: duplicate normalization in each provider service. Rejected because it can produce different identities for equivalent provider values.

### Use EF Core tracked upserts under one operation transaction

Replace provider-specific raw SQL `MERGE` statements with a common persistence path that resolves provider identity, checks both provider-scoped unique keys, inserts or updates the IOC, and replaces tag associations. Perform the dataset write in one explicit transaction so IOC and tag changes do not become partially inconsistent.

Alternative: retain separate provider-specific SQL statements. Rejected because they duplicate common identity and relationship behavior and couple services to physical tables.

### Preserve the existing API and MediatR surface

The three existing routes and their command/handler pairs remain unchanged. Their response counts represent provider records successfully processed through the common model. Only the service-side mapping and persistence behavior changes.

Alternative: replace these routes with the future administration API. Rejected because that would require features and infrastructure absent from the current repository.

## Risks / Trade-offs

- [The migration destroys all existing IOC data] -> Name the migration clearly, document the development-only assumption, and restore data by re-running the existing provider operations.
- [Two provider identity keys resolve to different existing rows] -> Treat the input as invalid and roll back the operation instead of merging ambiguous records.
- [Normalization changes the number of stored rows] -> Test equivalent inputs, ports, provider boundaries, and re-fetches against the specified unique keys.
- [Large provider files tracked in one DbContext increase memory use] -> Process records in bounded chunks while retaining one outer transaction and clear tracked entities between chunks.
- [Tag replacement can delete shared tags accidentally] -> Replace only join rows for the current IOC; do not delete shared `tags` records during ingestion.

## Migration Plan

1. Replace the Domain model and EF configurations, then generate one migration from the resulting model.
2. In the migration, drop the legacy schemas/tables and create the common tables, constraints, indexes, and seeded provider rows.
3. Apply the migration manually to the development database.
4. Re-fetch ThreatFox and PhishTank through the existing endpoints to repopulate the common model.
5. Roll back by reverting the code and applying the migration `Down` path; discarded provider data must still be re-fetched.
