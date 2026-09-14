import {
  adminFetchHistoryItemSchema,
  adminLogEntrySchema,
  adminProviderDashboardItemSchema,
} from './adminSchemas'

describe('admin response schemas', () => {
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
})
