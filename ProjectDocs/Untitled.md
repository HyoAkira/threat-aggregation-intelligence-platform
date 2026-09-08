# IOC Schema Notes

These notes were resolved into `PRD.md` and `Technical-Specification.md` on 2026-09-08:

- Provider IOC rows retain source-specific first/last-seen metadata.
- Tags are normalized for filtering and remain attributed to the provider row.
- Verification and other provider-specific fields are shown under their source in grouped results.
- PhishTank details are retained as complete JSON.
- Records are retained; `isFalsePositive` is calculated when `updatedAt` is older than 180 days.
- Provider details links and provider-native record IDs are retained.
- MISP is not part of the current schema implementation scope.
