## Context

See `proposal.md` for motivation. Today the guard matrix, cooldowns, minimum intervals, and advertised modes live in provider-name `switch` expressions (`FetchGuardEvaluator`, `AdminSettingsController`, `AdminProviderOperationsReader`), and `POST /api/admin/providers/{id}/fetch` always requests `recent`, so PhishTank's default fetch is rejected as unsupported. Provider execution is routed by numeric provider id (ThreatFox `1`, PhishTank `2`), and that routing stays unchanged by this change.

## Goals / Non-Goals

**Goals:**
- Persist per-provider fetch configuration and make the guard, settings validation, and admin read model consume it.
- Resolve the default mode from the provider so `/fetch` is correct for full-only providers.
- Provide admin create/update/delete endpoints whose validation matches the domain invariants.

**Non-Goals:**
- Dynamic loading of new parser/executor adapters. Adding a genuinely new integration still requires code; `IProviderFetchExecutor` remains routed by provider id, and an id with no registered adapter keeps using `UnavailableProviderFetchExecutor`.
- Changing secrets handling, authentication, or the scheduler cadence mechanics.
- Persisting capability configuration in a normalized child table (see Decisions).

## Decisions

### Store modes as a `[Flags]` mask on `providers`
Add a `[Flags] ProviderFetchModes` enum (`None=0`, `Recent=1`, `Full=2`) persisted as `int supportedModes`, plus `tinyint defaultMode`. `Provider.SupportedModes` exposes the mask and `HasMode(FetchMode)` maps a `FetchMode` to its bit. Reads derive the `supportedModes` token list by iterating the defined `FetchMode` values, and the dashboard item carries the same configuration fields as the write request (same names) so a listed provider round-trips through `PUT` without translation.
- Why: only two modes exist; a mask needs no join or extra rows and stays extensible to the full byte range. A `providerFetchModes` child table was considered but adds an aggregate, a migration table, and ordering concerns for no current benefit. A comma-separated string was rejected as harder to constrain and index.

### Per-mode cooldown and minimum interval columns
Add nullable `recentCooldownMinutes` / `fullCooldownMinutes` and non-null `minimumIntervalMinutes` to `providers`. Null cooldown means no cooldown for that mode.
- Why: matches the fixed mode set and keeps guard math a single-row read. A generic `(mode, cooldownMinutes)` table was rejected for the same reason as above.

### Extend `Provider`, keep construction compatible
Add the configuration to the `Provider` constructor while keeping the existing parameter order for the first three arguments and giving the new parameters defaults matching the current ThreatFox behavior. Add `HasMode`, `CooldownFor(FetchMode)`, and setters used by update. Guard against empty mode sets, a default mode outside the supported set, and non-positive cooldowns/minimum intervals.
- Why: minimizes churn in tests and call sites while making invalid states unconstructable.

### Guard consumes provider configuration; reservation carries the resolved mode
`FetchGuardEvaluator.Supports` delegates to `provider.HasMode(mode)` and `Cooldown` delegates to `provider.CooldownFor(mode)`. To fix the default-mode path without a second provider read, `IFetchGuard.ReserveAsync` accepts `FetchMode? mode`; when null the guard resolves `provider.DefaultMode` inside the existing serializable transaction, and `FetchReservation` gains the resolved `Mode`. `AdminProviderFetchController.Fetch` passes null and builds the `FetchJob` from `reservation.Mode`.
- Alternatives: a separate `GetDefaultModeAsync` read (extra round trip, racy relative to the guard transaction) or a controller-side provider query (duplicated loading) were rejected.

### Admin writes in a dedicated repository behind the existing controller route
Add `IAdminProviderRepository` (`CreateAsync`, `UpdateAsync`, `DeleteAsync`) implemented in Infrastructure and used by `AdminProviderOperationsController` (`POST /api/admin/providers`, `PUT/DELETE /api/admin/providers/{id}`). Requests use the same lowercase mode tokens as the read contract; a `FetchTokens.ParseMode` helper reverses `FetchTokens.Mode`. Responses reuse `AdminProviderDashboardItem`.
- Why: keeps the existing no-tracking read model separate from the write path while sharing the `api/admin` route so the existing `AdminControllerConvention` enforces the Admin role.

### Delete is referentially safe
`DELETE` returns 409 when `fetchHistory` or `iocs` still reference the provider, because both relationships cascade and a delete would silently destroy ingested data.
- Alternative: cascade delete (data loss) or soft delete/hide flag (a larger model change) were rejected.

### Migration backfills the seeded providers
A new EF migration adds the columns and executes a targeted `UPDATE ... WHERE id IN (1,2)` backfill so already-seeded databases get the documented defaults. Non-null columns use defaults (`supportedModes` 0, `defaultMode` 0, `minimumIntervalMinutes` 0) for the add-column step.
- Why: the seeder is insert-if-absent and will not touch existing rows.

## Risks / Trade-offs

- [Existing non-seeded providers receive `supportedModes = 0` and become unusable] → Rare for this deterministic two-provider deployment; document that such rows must be updated via `PUT`, and the entity/update validation prevents re-saving an empty set.
- [`ReserveAsync` signature and `FetchReservation` shape change] → Contained to `IFetchGuard` implementations and stubs; update the fetch controller tests alongside.
- [Guard code and write validation can drift] → Have both map modes through the shared `FetchTokens.ParseMode`/`FetchTokens.Mode` helpers and validate in the domain constructor.
- [Migration generation requires `dotnet ef`] → Generate the migration with the existing design-time factory; if the tool is unavailable, hand-write the migration and snapshot update and verify by applying to a fresh and an existing database.

## Migration Plan

1. Add the enum, entity fields, EF configuration, and migration (with backfill).
2. Update `ProviderSeeder` inserts to include the configuration columns.
3. Refactor `FetchGuardEvaluator`, `AdminProviderOperationsReader`, `AdminSettingsController`, and the fetch controller.
4. Add the admin write repository, DTOs, and endpoints.
5. Migration runs automatically at host startup through `DatabaseInitializer`; API and Worker must deploy together so both consume the same guard semantics.
6. Rollback: revert the code; the added columns are inert, or apply the migration `Down` to drop them after code rollback.
