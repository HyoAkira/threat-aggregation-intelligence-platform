## Why

The ThreatFox API returns reference URLs (particularly URL-encoded search queries from sources like Censys) that exceed the 1000-character limit on the `Reference` column in the `ThreatFox` table. This causes a SQL truncation error (`String or binary data would be truncated`) when attempting to download and store the full dataset.

## What Changes

- Increase the `Reference` column length from `nvarchar(1000)` to `nvarchar(max)` in the `ThreatFox` table
- Update the EF Core entity configuration to use `HasColumnType("nvarchar(max)")` instead of `HasMaxLength(1000)`
- Create and apply a database migration to alter the column type

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- `persistence/threatfox`: The `Reference` field requirement changes from max 1000 characters to unlimited length

## Impact

**Affected Code:**
- `ThreatIntelligence.Infrastructure/EntityTypeConfigurations/ThreatFoxEntityConfiguration.cs` - Line 25
- `ThreatIntelligence.Infrastructure/Migrations/20260904181846_IncreaseReferenceColumnLength.cs` - New migration
- Database schema: `providers.ThreatFox.Reference` column type changed

**APIs:** None (internal storage change only)

**Dependencies:** None

**Systems:** Database schema migration required