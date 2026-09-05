## Context

See proposal.md - Why. The ThreatFox API returns reference URLs that exceed 1000 characters, causing SQL truncation errors on insert.

Current state:
- `ThreatFoxEntityConfiguration.cs` line 25: `builder.Property(t => t.Reference).HasMaxLength(1000);`
- Migration `20260904102535_Initial.cs` line 76: `Reference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)`
- Database column: `providers.ThreatFox.Reference nvarchar(1000) NULL`
- The `Tags` column already uses `nvarchar(max)` (line 26 in entity config)

## Goals / Non-Goals

**Goals:**
- Allow Reference column to store URLs of any length without truncation
- Keep the change minimal and focused
- Match the pattern used for the `Tags` column

**Non-Goals:**
- Modify any other column lengths
- Change API response parsing logic
- Add new fields or tables

## Decisions

**Decision: Use `nvarchar(max)` for Reference column**

Rationale:
- The `Tags` column already uses this pattern (line 26 in entity config)
- ThreatFox reference URLs can be arbitrarily long (URL-encoded search queries from Censys, etc.)
- No practical upper bound on URL length from external sources
- `nvarchar(max)` is the standard SQL Server type for unbounded text

Alternatives considered:
- `HasMaxLength(4000)` - arbitrary limit, doesn't solve the fundamental issue
- `HasMaxLength(8000)` - still arbitrary, and SQL Server's max for in-row storage is 8000 bytes

**Decision: Create explicit migration**

Rationale:
- EF Core can detect the change, but explicit migration ensures controlled deployment
- Migration `20260904181846_IncreaseReferenceColumnLength` already created and applied
- Down migration available for rollback

## Risks / Trade-offs

- [Storage increase] → `nvarchar(max)` may use slightly more storage for very long values, but Reference is nullable and typically short; impact negligible
- [Performance] → Very long values stored off-row in LOB pages; acceptable for this low-frequency column
- [Rollback] → Down migration changes back to `nvarchar(1000)` - data loss possible if any values exceed 1000 chars; only safe if no long values exist

## Migration Plan

1. Entity config change: `HasMaxLength(1000)` → `HasColumnType("nvarchar(max)")` (already done)
2. Migration created: `20260904181846_IncreaseReferenceColumnLength` (already done)
3. Migration applied to database (already done)
4. Verify build succeeds and tests pass (if any)

## Open Questions

None - implementation complete and verified.