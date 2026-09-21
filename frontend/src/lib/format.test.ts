import {
  parseUtcDateTime,
  formatLocalDateTime,
  formatLocalDateTimeShort,
  formatLocalMonthDayTime,
} from './format'

// jest.config.js pins TZ=UTC so local-time presentation is deterministic here.
// In the browser these helpers render in the viewer's own timezone instead.

describe('parseUtcDateTime', () => {
  it('treats an offsetless value as UTC', () => {
    expect(parseUtcDateTime('2026-09-15T10:00:00').getTime()).toBe(
      parseUtcDateTime('2026-09-15T10:00:00Z').getTime(),
    )
  })

  it('parses a Z-suffixed value', () => {
    expect(parseUtcDateTime('2026-09-15T10:00:00Z').toISOString()).toBe(
      '2026-09-15T10:00:00.000Z',
    )
  })

  it('honors an explicit offset', () => {
    expect(parseUtcDateTime('2026-09-15T15:00:00+05:00').toISOString()).toBe(
      '2026-09-15T10:00:00.000Z',
    )
  })
})

describe('formatLocalDateTime', () => {
  it('renders an offsetless instant in the runtime timezone', () => {
    expect(formatLocalDateTime('2026-09-15T10:00:00')).toBe(
      '2026-09-15 10:00:00 UTC',
    )
  })

  it('renders an explicit offset as the same instant', () => {
    expect(formatLocalDateTime('2026-09-15T15:00:00+05:00')).toBe(
      '2026-09-15 10:00:00 UTC',
    )
  })

  it('returns null for null, undefined, empty, and invalid values', () => {
    expect(formatLocalDateTime(null)).toBeNull()
    expect(formatLocalDateTime(undefined)).toBeNull()
    expect(formatLocalDateTime('')).toBeNull()
    expect(formatLocalDateTime('not-a-date')).toBeNull()
  })
})

describe('formatLocalDateTimeShort', () => {
  it('renders a compact runtime-timezone timestamp', () => {
    expect(formatLocalDateTimeShort('2026-09-15T10:00:00Z')).toBe(
      '2026-09-15 10:00',
    )
  })

  it('returns null for empty and invalid values', () => {
    expect(formatLocalDateTimeShort(null)).toBeNull()
    expect(formatLocalDateTimeShort('not-a-date')).toBeNull()
  })
})

describe('formatLocalMonthDayTime', () => {
  it('renders offsetless input in the runtime timezone', () => {
    expect(formatLocalMonthDayTime('2026-09-15T10:00:00')).toBe('Sep 15, 10:00')
  })

  it('renders an explicit offset as the same instant', () => {
    expect(formatLocalMonthDayTime('2026-09-15T15:00:00+05:00')).toBe(
      'Sep 15, 10:00',
    )
  })

  it('returns null for null, undefined, empty, and invalid values', () => {
    expect(formatLocalMonthDayTime(null)).toBeNull()
    expect(formatLocalMonthDayTime(undefined)).toBeNull()
    expect(formatLocalMonthDayTime('')).toBeNull()
    expect(formatLocalMonthDayTime('not-a-date')).toBeNull()
  })
})
