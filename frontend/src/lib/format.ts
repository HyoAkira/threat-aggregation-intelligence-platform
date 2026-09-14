/**
 * Parses an API UTC instant deterministically. `DateTime` values may serialize
 * without an offset suffix; those are UTC by contract, so a missing offset is
 * added instead of letting `Date` fall back to the machine's local zone.
 */
export function parseUtcDateTime(value: string): Date {
  if (/[Zz]|[+-]\d{2}:\d{2}$/.test(value)) return new Date(value)
  return new Date(`${value}Z`)
}

/** Stable, locale-independent UTC presentation, identical on server and client. */
export function formatUtcDateTime(
  value: string | null | undefined,
): string | null {
  if (value === null || value === undefined || value === '') return null
  const date = parseUtcDateTime(value)
  if (Number.isNaN(date.getTime())) return null
  return `${date.toISOString().slice(0, 10)} ${date.toISOString().slice(11, 19)} UTC`
}

/**
 * Presentation for source field values. Null/undefined yields null (the caller
 * omits the row entirely); stored `false`, `0`, and empty values are valid data
 * and stay visible and understandable.
 */
export function formatFieldValue(value: unknown): string | null {
  if (value === null || value === undefined) return null
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (typeof value === 'number') return String(value)
  if (typeof value === 'string') return value === '' ? '(empty)' : value
  return JSON.stringify(value, null, 2)
}

/** False-positive presentation derived from the per-source `isFalsePositive` flag. */
export function formatFalsePositive(isFalsePositive: boolean): string {
  return isFalsePositive ? 'Marked false positive' : 'Not marked false positive'
}
