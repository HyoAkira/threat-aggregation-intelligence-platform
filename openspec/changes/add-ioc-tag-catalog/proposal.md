## Why

IOC browse and search already accept a normalized tag filter, but users have no way to discover valid tag names. They must guess provider vocabulary and casing, so an existing query capability is effectively unusable.

## What Changes

- Add a public `GET /api/iocs/tags` catalog endpoint with required, bounded `limit` and optional autocomplete query `q`.
- Return normalized tag names with the number of IOC-tag association rows using each tag, ordered by usage and then name.
- Expose the catalog through the shared IOC query reader and cover validation, filtering, ordering, and empty-result behavior with API tests.
- Add typed frontend access and autocomplete suggestions to the browse and search tag filters without changing their URL/filter contracts.

## Capabilities

### New Capabilities

- `ioc-tag-catalog`: Public discovery of normalized IOC tags with bounded usage counts and autocomplete filtering.

### Modified Capabilities

None. Existing browse and search filtering requirements remain unchanged; their controls only gain a discovery aid.

## Impact

- API: `IocsController`, `IIocQueryReader`, and `IocQueryReader`.
- Tests: IOC API integration coverage and frontend API/component coverage.
- Frontend: typed schema/client/query key plus a shared tag autocomplete control used by browse and search.
- Persistence: read-only aggregation over existing `tags` and `iocTags` tables; no migration.
