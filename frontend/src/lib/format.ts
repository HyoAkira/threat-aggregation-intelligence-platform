/**
 * Parses an API UTC instant deterministically. `DateTime` values may serialize
 * without an offset suffix; those are UTC by contract, so a missing offset is
 * added instead of letting `Date` fall back to the machine's local zone.
 */
export function parseUtcDateTime(value: string): Date {
  if (/[Zz]|[+-]\d{2}:\d{2}$/.test(value)) return new Date(value)
  return new Date(`${value}Z`)
}

function pad2(value: number): string {
  return String(value).padStart(2, '0')
}

/**
 * Short label of the runtime's local timezone for a given instant, e.g. "GMT+3"
 * or "UTC". Presentation only; the underlying instant is never altered.
 */
export function localTimeZoneLabel(date: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZoneName: 'short',
  }).formatToParts(date)
  return parts.find((part) => part.type === 'timeZoneName')?.value ?? ''
}

/**
 * Presentation in the viewer's browser timezone, e.g. "2026-09-15 13:00:00 GMT+3".
 * API instants are always UTC; they are parsed deterministically and then rendered
 * with the runtime's local zone so each viewer sees their own wall-clock time.
 */
export function formatLocalDateTime(
  value: string | null | undefined,
): string | null {
  if (value === null || value === undefined || value === '') return null
  const date = parseUtcDateTime(value)
  if (Number.isNaN(date.getTime())) return null
  const stamp = `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())} ${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`
  const label = localTimeZoneLabel(date)
  return label ? `${stamp} ${label}` : stamp
}

/** Compact viewer-local presentation for dense cells, e.g. "2026-09-15 13:00". */
export function formatLocalDateTimeShort(
  value: string | null | undefined,
): string | null {
  if (value === null || value === undefined || value === '') return null
  const date = parseUtcDateTime(value)
  if (Number.isNaN(date.getTime())) return null
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())} ${pad2(date.getHours())}:${pad2(date.getMinutes())}`
}

/** Compact viewer-local presentation, e.g. "Sep 15, 13:00". */
export function formatLocalMonthDayTime(
  value: string | null | undefined,
): string | null {
  if (value === null || value === undefined || value === '') return null
  const date = parseUtcDateTime(value)
  if (Number.isNaN(date.getTime())) return null
  return date.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
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
