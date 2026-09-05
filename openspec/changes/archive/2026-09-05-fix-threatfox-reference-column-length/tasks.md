## 1. Entity Configuration Update

- [x] 1.1 Update `ThreatFoxEntityConfiguration.cs` to use `HasColumnType("nvarchar(max)")` for Reference column - **Verify**: Build succeeds

## 2. Database Migration

- [x] 2.1 Create migration `IncreaseReferenceColumnLength` - **Verify**: Migration file generated
- [x] 2.2 Apply migration to database - **Verify**: `dotnet ef database update` succeeds
- [x] 2.3 Verify database schema change - **Verify**: `Reference` column is now `nvarchar(max)` in `providers.ThreatFox`

## 3. Verification

- [x] 3.1 Build entire solution - **Verify**: `dotnet build` succeeds with 0 errors
- [x] 3.2 Test ThreatFox dataset download endpoint - **Verify**: Download completes without truncation error
- [x] 3.3 Verify existing data integrity - **Verify**: Existing ThreatFox rows still queryable

## 4. Documentation

- [x] 4.1 Update README.md if needed - **Verify**: README reflects current state (no changes needed)