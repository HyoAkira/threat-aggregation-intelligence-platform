## Why

Provider capability and cadence rules are hard-coded by provider name in `FetchGuardEvaluator`, `AdminProviderOperationsReader`, `AdminSettingsController`, and the seeder, so the advertised modes, cooldowns, minimum intervals, and the default mode cannot be changed without a code change and adding a provider requires editing switches. Moving this configuration into the `providers` table makes provider capabilities data-driven and enables an admin management endpoint.

## What Changes

- Add provider-configuration columns to the `providers` table: `defaultMode`, `supportedModes` (mode bitmask), `recentCooldownMinutes`, `fullCooldownMinutes`, and `minimumIntervalMinutes`.
- Refactor the trigger guard to read capabilities, cooldowns, and enabled state from the provider row instead of a provider-name switch.
- Source `supportedModes` and the other configuration fields for `GET /api/admin/providers` from the persisted provider configuration, using the same field names as the write endpoints so a listed provider can be edited and PUT back without translation.
- Source the per-provider minimum interval for runtime settings validation from the provider row.
- Route `POST /api/admin/providers/{id}/fetch` through the provider's persisted default mode (fixes PhishTank default fetches, which currently request `recent` and are rejected).
- Add admin write endpoints: `POST /api/admin/providers`, `PUT /api/admin/providers/{id}`, and `DELETE /api/admin/providers/{id}`.
- Update the provider seeder to persist the capability/cooldown/interval defaults for the two deterministic providers.
- Add an EF Core migration that creates the new columns and backfills the two seeded providers.

## Capabilities

### New Capabilities
- `admin-provider-management`: Admin create/update/delete endpoints for provider configuration, with validation of modes, intervals, cooldowns, and referential safety.

### Modified Capabilities
- `trigger-guards`: capability, cooldown, and default-mode rules are derived from persisted provider configuration instead of provider-name switches.
- `domain-model`: the `Provider` entity gains configuration invariants for default/supported modes, cooldowns, and minimum interval.
- `persistence`: the `providers` table gains the configuration columns and a deterministic migration that backfills existing seeded rows.
- `seeding`: the seeded providers include their capability, cooldown, and minimum-interval defaults.
- `manual-fetch-triggers`: the default `/fetch` endpoint uses the provider's persisted default mode rather than the RECENT mode for every provider.

## Impact

- Domain: `ThreatIntelligence.Domain/Entities/Provider.cs`, `FetchMode` usage.
- Infrastructure: `FetchGuardEvaluator`, `AdminProviderOperationsReader`, `ProviderSeeder`, `ProviderConfiguration`, new migration, provider management repository.
- API: `AdminProviderFetchController` (default mode), new write endpoints, `AdminSettingsController` (minimum interval).
- Contracts: `AdminProviderDashboardItem` gains `defaultMode`, the per-mode cooldowns, and `minimumIntervalMinutes`, and renames its mode list to `supportedModes` to mirror the write request; new create/update request/response DTOs.
- No changes to admin authentication or secrets handling.
