## Why

The current ingestion path stores ThreatFox and PhishTank records in separate provider-specific entities and tables, which prevents the existing services from building the common provider-attributed IOC dataset required by the project. Replacing those raw stores now creates a small, consistent persistence foundation without introducing unrelated product features.

## What Changes

- Remove the provider-specific `ThreatFox` and `PhishTank` domain entities and database tables.
- Replace them with the common `Provider`, `Ioc`, `Tag`, and `IocTag` persistence model, using the field definitions and constraints already established in the PRD and Technical Specification.
- Change ThreatFox and PhishTank ingestion services to map provider records into the common model, normalize identity values, and upsert provider-owned IOC rows and current tags.
- Keep the existing MediatR commands and three HTTP endpoints, but make their operations persist through the common model.
- Replace the current schema through a destructive development migration; existing data is intentionally discarded and restored by re-fetching providers.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `persistence/provider-schema`: Replace provider-specific raw storage with the common provider-owned IOC and normalized tag model, including provider-scoped upsert identity.
- `persistence/ef-core-dbcontext`: Replace raw-table entity sets and migration output with the common persistence model.

## Impact

Affected areas are Domain entities, EF Core configurations and migrations, `ThreatIntelDbContext`, both provider services, and tests around persistence and the existing ingestion operations. Existing endpoint paths, commands, response DTOs, application layering, configuration, and unrelated future product features remain unchanged.
