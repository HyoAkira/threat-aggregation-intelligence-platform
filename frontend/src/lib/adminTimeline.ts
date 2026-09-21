/**
 * Browser timezone and local-calendar range helpers for the Admin IOC timeline.
 *
 * The IANA timezone is the authoritative grouping context: presets and custom dates are
 * computed as local calendar boundaries, converted to canonical UTC instants for transport,
 * and displayed back with the same timezone. A fixed numeric offset is never sent in place of
 * the timezone because it cannot represent DST transitions across a multi-day range.
 */

export const FALLBACK_TIME_ZONE = 'UTC'
export const MAX_TIMELINE_DAYS = 31

export type TimelinePreset = '7d' | '1m' | 'custom'

export type TimelineRange = {
  /** Inclusive local start date (YYYY-MM-DD). */
  startDate: string
  /** Inclusive local end date (YYYY-MM-DD). */
  endDate: string
  /** Canonical UTC instant of the local start midnight. */
  from: string
  /** Canonical UTC instant of the local end-exclusive midnight. */
  to: string
  /** Number of local calendar days covered. */
  days: number
}

export const PRESET_DAYS: Record<'7d' | '1m', number> = {
  '7d': 7,
  '1m': 30,
}

/** Detects the browser IANA timezone, falling back explicitly to UTC. */
export function detectTimeZone(): string {
  try {
    const zone = Intl.DateTimeFormat().resolvedOptions().timeZone
    return typeof zone === 'string' && zone.length > 0
      ? zone
      : FALLBACK_TIME_ZONE
  } catch {
    return FALLBACK_TIME_ZONE
  }
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

function readParts(
  formatter: Intl.DateTimeFormat,
  date: Date,
): Record<string, number> {
  const values: Record<string, number> = {}
  for (const part of formatter.formatToParts(date)) {
    if (part.type !== 'literal') values[part.type] = Number(part.value)
  }
  return values
}

/** Offset of `timeZone` from UTC at the given instant, in minutes. */
function timeZoneOffsetMinutes(timeZone: string, date: Date): number {
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
  const values = readParts(formatter, date)
  const asUtc = Date.UTC(
    values.year,
    values.month - 1,
    values.day,
    values.hour % 24,
    values.minute,
    values.second,
  )
  return Math.round((asUtc - date.getTime()) / 60000)
}

/** Formats the offset of `timeZone` as `UTC±HH:MM`, including fractional offsets. */
export function formatUtcOffset(
  timeZone: string,
  at: Date = new Date(),
): string {
  const total = timeZoneOffsetMinutes(timeZone, at)
  const sign = total >= 0 ? '+' : '-'
  const absolute = Math.abs(total)
  return `UTC${sign}${pad(Math.floor(absolute / 60))}:${pad(absolute % 60)}`
}

/** Local calendar date (YYYY-MM-DD) of an instant in the requested timezone. */
export function localDateInTimeZone(
  date: Date,
  timeZone: string,
): string {
  const values = readParts(
    new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }),
    date,
  )
  return `${values.year}-${pad(values.month)}-${pad(values.day)}`
}

/** Shifts a YYYY-MM-DD calendar date by whole days without timezone involvement. */
export function shiftLocalDate(localDate: string, days: number): string {
  const [year, month, day] = localDate.split('-').map(Number)
  const shifted = new Date(Date.UTC(year, month - 1, day + days))
  return `${shifted.getUTCFullYear()}-${pad(shifted.getUTCMonth() + 1)}-${pad(
    shifted.getUTCDate(),
  )}`
}

/**
 * Canonical UTC instant of local midnight for a YYYY-MM-DD date. Uses a two-pass offset
 * lookup so a DST transition at midnight still resolves to the correct instant.
 */
export function localMidnightUtc(
  localDate: string,
  timeZone: string,
): Date {
  const [year, month, day] = localDate.split('-').map(Number)
  const utcGuess = Date.UTC(year, month - 1, day)
  const firstOffset = timeZoneOffsetMinutes(timeZone, new Date(utcGuess))
  let candidate = utcGuess - firstOffset * 60000
  const secondOffset = timeZoneOffsetMinutes(timeZone, new Date(candidate))
  if (secondOffset !== firstOffset) {
    candidate = utcGuess - secondOffset * 60000
  }
  return new Date(candidate)
}

/** Number of inclusive local calendar days between two YYYY-MM-DD dates. */
export function inclusiveDayCount(
  startDate: string,
  endDate: string,
): number {
  const [y1, m1, d1] = startDate.split('-').map(Number)
  const [y2, m2, d2] = endDate.split('-').map(Number)
  const start = Date.UTC(y1, m1 - 1, d1)
  const end = Date.UTC(y2, m2 - 1, d2)
  return Math.round((end - start) / 86400000) + 1
}

/** Builds a bounded UTC transport range from inclusive local calendar dates. */
export function rangeFromLocalDates(
  startDate: string,
  endDate: string,
  timeZone: string,
): TimelineRange {
  return {
    startDate,
    endDate,
    from: localMidnightUtc(startDate, timeZone).toISOString(),
    to: localMidnightUtc(shiftLocalDate(endDate, 1), timeZone).toISOString(),
    days: inclusiveDayCount(startDate, endDate),
  }
}

/** Preset range ending on the user's current local date. */
export function computePresetRange(
  preset: '7d' | '1m',
  timeZone: string,
  at: Date = new Date(),
): TimelineRange {
  const endDate = localDateInTimeZone(at, timeZone)
  const startDate = shiftLocalDate(endDate, -(PRESET_DAYS[preset] - 1))
  return rangeFromLocalDates(startDate, endDate, timeZone)
}

export type CustomRangeValidation = {
  valid: boolean
  error: string | null
}

const datePattern = /^\d{4}-\d{2}-\d{2}$/

/** Validates a custom local-date range before any timeline request is issued. */
export function validateCustomRange(
  startDate: string | null | undefined,
  endDate: string | null | undefined,
): CustomRangeValidation {
  if (!startDate || !endDate) {
    return { valid: false, error: 'Select both a start and end date.' }
  }
  if (!datePattern.test(startDate) || !datePattern.test(endDate)) {
    return { valid: false, error: 'Enter valid calendar dates.' }
  }
  if (startDate > endDate) {
    return {
      valid: false,
      error: 'The start date must be on or before the end date.',
    }
  }
  if (inclusiveDayCount(startDate, endDate) > MAX_TIMELINE_DAYS) {
    return {
      valid: false,
      error: `Select a range of at most ${MAX_TIMELINE_DAYS} days.`,
    }
  }
  return { valid: true, error: null }
}
