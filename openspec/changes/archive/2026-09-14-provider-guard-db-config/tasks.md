## 1. Domain model and tests

- [x] 1.1 Add `ProviderFetchModes` flags enum and failing unit tests for `Provider` construction invariants (blank name/URL, non-positive interval, empty supported set, default mode outside supported set, non-positive cooldown, non-positive minimum interval); run `dotnet test tests/ThreatIntelligence.UnitTests` and confirm the new tests fail
- [x] 1.2 Implement `Provider` fields (`DefaultMode`, `SupportedModes`, `RecentCooldownMinutes`, `FullCooldownMinutes`, `MinimumIntervalMinutes`), `HasMode`, `CooldownFor`, and update setters; confirm the 1.1 tests pass and existing `Provider` usages still compile

## 2. Persistence, migration, and seeding

- [x] 2.1 Map the configuration columns in `ProviderConfiguration` (`defaultMode` tinyint, `supportedModes` int, nullable cooldowns, `minimumIntervalMinutes` int) and verify the model builds
- [x] 2.2 Generate the `AddProviderFetchConfiguration` EF migration and hand-add the `UPDATE` backfill for providers `1` and `2` with the documented defaults; verify applying it succeeds on a fresh and an already-migrated database
- [x] 2.3 Update `ProviderSeeder` inserts to include the configuration columns with the documented ThreatFox/PhishTank defaults; verify a seeded fresh database returns the expected capabilities from `GET /api/admin/providers`

## 3. Guard refactor (red → green)

- [x] 3.1 Add failing unit tests proving `FetchGuardEvaluator.Supports` and cooldown behavior derive from provider fields (supported/unsupported modes, null cooldown, updated configuration) and run the unit tests to confirm failure
- [x] 3.2 Refactor `FetchGuardEvaluator` to read `Provider.HasMode` and `Provider.CooldownFor`; confirm the 3.1 tests and existing `FetchGuardEvaluatorTests` pass

## 4. Default-mode routing

- [x] 4.1 Update `IFetchGuard.ReserveAsync` to accept `FetchMode?` and carry the resolved `Mode` on `FetchReservation`; add a failing integration test asserting a full-only provider's default `/fetch` queues `full`
- [x] 4.2 Implement default-mode resolution inside the reservation transaction and update `AdminProviderFetchController.Fetch` plus test stubs; confirm the 4.1 test passes

## 5. Admin read model and settings

- [x] 5.1 Add `defaultMode` to `AdminProviderDashboardItem` and source `supportedModes`/`defaultMode` from persisted configuration in `AdminProviderOperationsReader`; update `AdminProviderOperationsControllerTests` and confirm they pass
- [x] 5.2 Replace `AdminSettingsController.MinimumInterval` with `Provider.MinimumIntervalMinutes`; add/adjust tests asserting an interval below the stored minimum is rejected and confirm they pass
- [x] 5.3 Rename the dashboard mode list to `supportedModes` and expose `recentCooldownMinutes`, `fullCooldownMinutes`, and `minimumIntervalMinutes` on `AdminProviderDashboardItem` so the read item mirrors the write request; add failing tests for the full-configuration read and a GET→PUT round trip, then confirm they pass

## 6. Admin provider management endpoints (red → green)

- [x] 6.1 Add `FetchTokens.ParseMode` and failing unit tests for token round-tripping and unknown tokens
- [x] 6.2 Add `IAdminProviderRepository` plus create/update/delete request DTOs and implement `AdminProviderRepository` with domain-invariant and duplicate-name validation; verify with integration tests against SQL Server
- [x] 6.3 Add `POST`, `PUT`, and `DELETE /api/admin/providers` endpoints using the repository and verify 201/200/204, 400 invalid input, 404 unknown, 409 referenced-provider deletion, and 403 for non-admin callers
- [x] 6.4 Verify an update to `supportedModes` changes the reported mode list on the next `GET /api/admin/providers`

## 7. System verification

- [x] 7.1 Run the full unit and integration test suites (`dotnet test ThreatIntelligence.slnx`) and confirm all pass
- [x] 7.2 Run `openspec validate provider-guard-db-config --strict` and confirm the change is valid
- [x] 7.3 Start the API against a migrated database and manually verify the provider dashboard, a default PhishTank fetch, and a create/update/delete round trip
