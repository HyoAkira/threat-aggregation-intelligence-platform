import {
  adminFetchHistoryItemSchema,
  adminLogEntrySchema,
  adminProviderDashboardItemSchema,
  adminTimelineSchema,
  settingsResponseSchema,
} from './adminSchemas'

describe('admin response schemas', () => {
  it('accepts provider minimum intervals in settings responses', () => {
    const settings = settingsResponseSchema.parse({
      logLevel: 'Information',
      providers: [
        {
          id: 2,
          name: 'PhishTank',
          isEnabled: true,
          intervalMinutes: 480,
          minimumIntervalMinutes: 480,
        },
      ],
    })

    expect(settings.providers[0].minimumIntervalMinutes).toBe(480)
  })

  it('accepts provider null fields omitted by the API serializer', () => {
    const provider = adminProviderDashboardItemSchema.parse({
      id: 2,
      name: 'PhishTank',
      baseUrl: 'https://example.test',
      isEnabled: true,
      intervalMinutes: 480,
      supportedModes: ['full'],
      defaultMode: 'full',
      minimumIntervalMinutes: 480,
    })

    expect(provider.lastFetchStatus).toBeNull()
    expect(provider.lastFetchAt).toBeNull()
    expect(provider.nextFetchTime).toBeNull()
    expect(provider.recentCooldownMinutes).toBeNull()
    expect(provider.fullCooldownMinutes).toBeNull()
  })

  it('accepts ordinary log entries without optional exception metadata', () => {
    const entry = adminLogEntrySchema.parse({
      timestamp: '2026-09-14T11:20:43.8367037+00:00',
      level: 'Information',
      message: 'HTTP GET /health responded 200',
    })

    expect(entry.correlationId).toBeNull()
    expect(entry.exception).toBeNull()
  })

  it('accepts lowercase history tokens and omitted terminal fields', () => {
    const item = adminFetchHistoryItemSchema.parse({
      jobId: '117ac9f7-ff9b-4a13-bb0c-491b2a350031',
      providerId: 1,
      status: 'running',
      trigger: 'manual',
      mode: 'recent',
      startedAt: '2026-09-14T11:20:43Z',
      recordsAdded: 0,
      recordsUpdated: 0,
    })

    expect(item.finishedAt).toBeNull()
    expect(item.errorDetail).toBeNull()
  })

  it('accepts a valid timeline response with zero-filled points', () => {
    const timeline = adminTimelineSchema.parse({
      from: '2026-09-14T00:00:00Z',
      to: '2026-09-16T00:00:00Z',
      timeZone: 'Europe/Istanbul',
      hasSuccessfulFetches: true,
      points: [
        {
          localDate: '2026-09-14',
          bucketStartUtc: '2026-09-13T21:00:00Z',
          recordsAdded: 5,
          recordsUpdated: 2,
        },
        {
          localDate: '2026-09-15',
          bucketStartUtc: '2026-09-14T21:00:00Z',
          recordsAdded: 0,
          recordsUpdated: 0,
        },
      ],
    })

    expect(timeline.points).toHaveLength(2)
    expect(timeline.points[1].recordsAdded).toBe(0)
  })

  it('rejects a malformed timeline point', () => {
    const result = adminTimelineSchema.safeParse({
      from: '2026-09-14T00:00:00Z',
      to: '2026-09-16T00:00:00Z',
      timeZone: 'UTC',
      hasSuccessfulFetches: true,
      points: [
        {
          localDate: '2026/09/14',
          bucketStartUtc: '2026-09-13T21:00:00Z',
          recordsAdded: -1,
        },
      ],
    })

    expect(result.success).toBe(false)
  })
})
