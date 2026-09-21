import {
  FALLBACK_TIME_ZONE,
  MAX_TIMELINE_DAYS,
  computePresetRange,
  detectTimeZone,
  formatUtcOffset,
  inclusiveDayCount,
  localDateInTimeZone,
  localMidnightUtc,
  rangeFromLocalDates,
  shiftLocalDate,
  validateCustomRange,
} from './adminTimeline'

// jest.config.js forces TZ=UTC so local-time presentation is deterministic.

describe('detectTimeZone', () => {
  it('returns the resolved browser IANA timezone', () => {
    expect(detectTimeZone()).toBe('UTC')
  })

  it('falls back explicitly to UTC when no timezone can be obtained', () => {
    const spy = jest
      .spyOn(Intl, 'DateTimeFormat')
      .mockImplementation(() => {
        throw new Error('timezone unavailable')
      })
    try {
      expect(detectTimeZone()).toBe(FALLBACK_TIME_ZONE)
    } finally {
      spy.mockRestore()
    }
  })
})

describe('formatUtcOffset', () => {
  it('formats UTC as UTC+00:00', () => {
    expect(formatUtcOffset('UTC')).toBe('UTC+00:00')
  })

  it('formats a positive whole-hour offset', () => {
    expect(
      formatUtcOffset('Europe/Istanbul', new Date('2026-07-15T12:00:00Z')),
    ).toBe('UTC+03:00')
  })

  it('formats a fractional offset', () => {
    expect(
      formatUtcOffset('Asia/Kathmandu', new Date('2026-07-15T12:00:00Z')),
    ).toBe('UTC+05:45')
  })

  it('formats a negative fractional offset', () => {
    expect(
      formatUtcOffset('America/St_Johns', new Date('2026-01-15T12:00:00Z')),
    ).toBe('UTC-03:30')
  })

  it('reflects DST for the given instant', () => {
    expect(
      formatUtcOffset('America/New_York', new Date('2026-01-15T12:00:00Z')),
    ).toBe('UTC-05:00')
    expect(
      formatUtcOffset('America/New_York', new Date('2026-07-15T12:00:00Z')),
    ).toBe('UTC-04:00')
  })
})

describe('localMidnightUtc', () => {
  it('resolves UTC midnight for UTC', () => {
    expect(localMidnightUtc('2026-09-15', 'UTC').toISOString()).toBe(
      '2026-09-15T00:00:00.000Z',
    )
  })

  it('resolves a positive offset local midnight', () => {
    expect(localMidnightUtc('2026-09-15', 'Europe/Istanbul').toISOString()).toBe(
      '2026-09-14T21:00:00.000Z',
    )
  })

  it('resolves a fractional offset local midnight', () => {
    expect(localMidnightUtc('2026-09-15', 'Asia/Kathmandu').toISOString()).toBe(
      '2026-09-14T18:15:00.000Z',
    )
  })

  it('resolves DST-aware bucket starts across the spring-forward transition', () => {
    expect(
      localMidnightUtc('2026-03-08', 'America/New_York').toISOString(),
    ).toBe('2026-03-08T05:00:00.000Z')
    expect(
      localMidnightUtc('2026-03-09', 'America/New_York').toISOString(),
    ).toBe('2026-03-09T04:00:00.000Z')
  })
})

describe('local calendar helpers', () => {
  it('reads the local date of an instant in a timezone', () => {
    expect(
      localDateInTimeZone(
        new Date('2026-09-14T22:00:00Z'),
        'Europe/Istanbul',
      ),
    ).toBe('2026-09-15')
  })

  it('shifts calendar dates by whole days', () => {
    expect(shiftLocalDate('2026-09-15', -6)).toBe('2026-09-09')
    expect(shiftLocalDate('2026-12-31', 1)).toBe('2027-01-01')
  })

  it('counts inclusive local days', () => {
    expect(inclusiveDayCount('2026-09-15', '2026-09-15')).toBe(1)
    expect(inclusiveDayCount('2026-09-15', '2026-09-21')).toBe(7)
  })
})

describe('computePresetRange', () => {
  it('derives a seven-local-day range ending today', () => {
    const range = computePresetRange('7d', 'UTC', new Date('2026-09-21T10:00:00Z'))
    expect(range.startDate).toBe('2026-09-15')
    expect(range.endDate).toBe('2026-09-21')
    expect(range.days).toBe(7)
    expect(range.from).toBe('2026-09-15T00:00:00.000Z')
    expect(range.to).toBe('2026-09-22T00:00:00.000Z')
  })

  it('derives a thirty-local-day month range', () => {
    const range = computePresetRange('1m', 'UTC', new Date('2026-09-21T10:00:00Z'))
    expect(range.startDate).toBe('2026-08-23')
    expect(range.days).toBe(30)
  })
})

describe('rangeFromLocalDates', () => {
  it('converts local boundaries to canonical UTC instants', () => {
    const range = rangeFromLocalDates(
      '2026-09-15',
      '2026-09-21',
      'Europe/Istanbul',
    )
    expect(range.from).toBe('2026-09-14T21:00:00.000Z')
    expect(range.to).toBe('2026-09-21T21:00:00.000Z')
    expect(range.days).toBe(7)
  })
})

describe('validateCustomRange', () => {
  it('requires both dates', () => {
    expect(validateCustomRange('', '').valid).toBe(false)
    expect(validateCustomRange('2026-09-15', '').valid).toBe(false)
  })

  it('rejects an inverted range', () => {
    const result = validateCustomRange('2026-09-21', '2026-09-15')
    expect(result.valid).toBe(false)
    expect(result.error).toMatch(/start date/i)
  })

  it('rejects a range longer than the maximum', () => {
    const result = validateCustomRange('2026-08-01', '2026-09-01')
    expect(result.valid).toBe(false)
    expect(result.error).toContain(String(MAX_TIMELINE_DAYS))
  })

  it('accepts the maximum supported range', () => {
    expect(validateCustomRange('2026-08-01', '2026-08-31').valid).toBe(true)
  })

  it('accepts a valid bounded range', () => {
    expect(validateCustomRange('2026-09-15', '2026-09-21').valid).toBe(true)
  })
})
