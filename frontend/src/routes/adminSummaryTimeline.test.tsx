import { useAuthStore } from '../lib/authStore'
import { queryClient } from '../lib/queryClient'
import { adminSummaryKey, adminTimelineKey } from '../lib/queryKeys'
import { computePresetRange } from '../lib/adminTimeline'
import { makeSession } from '../test/fixtures'
import { renderAppRoute, resetClientState } from '../test/renderApp'

/** renderToString splits adjacent text/expression nodes with comment markers. */
function markupText(markup: string): string {
  return markup.replaceAll('<!-- -->', '')
}

const summary = { total: 12, ipv4: 5, ipv6: 2, domain: 3, url: 2 }

function seedTimeline(overrides: Partial<{ hasSuccessfulFetches: boolean }> = {}) {
  const range = computePresetRange('7d', 'UTC')
  queryClient.setQueryData(adminSummaryKey(), summary)
  queryClient.setQueryData(adminTimelineKey(range.from, range.to, 'UTC'), {
    from: range.from,
    to: range.to,
    timeZone: 'UTC',
    hasSuccessfulFetches: overrides.hasSuccessfulFetches ?? true,
    points: [
      {
        localDate: '2026-09-15',
        bucketStartUtc: '2026-09-15T00:00:00.000Z',
        recordsAdded: 5,
        recordsUpdated: 2,
      },
      {
        localDate: '2026-09-16',
        bucketStartUtc: '2026-09-16T00:00:00.000Z',
        recordsAdded: 0,
        recordsUpdated: 0,
      },
    ],
  })
}

describe('admin summary timeline', () => {
  beforeEach(() => {
    resetClientState()
    useAuthStore.setState({ session: makeSession({ role: 'Admin' }) })
  })

  afterEach(() => {
    resetClientState()
    useAuthStore.setState({ session: null })
  })

  it('renders the metric cards plus the timeline with an accessible data table', async () => {
    seedTimeline()

    const markup = markupText(await renderAppRoute('/admin/summary'))

    expect(markup).toContain('Database Summary')
    expect(markup).toContain('Total Indicators')
    expect(markup).toContain('IOC Activity Timeline')
    expect(markup).toContain('Times shown in UTC (UTC+00:00)')
    expect(markup).toContain('View timeline data table')
    expect(markup).toContain('2026-09-15')
    expect(markup).toContain('Added')
    expect(markup).toContain('Updated')
  })

  it('shows a no-activity state while retaining the selected range and metric cards', async () => {
    seedTimeline({ hasSuccessfulFetches: false })

    const markup = markupText(await renderAppRoute('/admin/summary'))

    expect(markup).toContain('No IOC activity was recorded')
    expect(markup).toContain('Total Indicators')
    expect(markup).toContain('Times shown in UTC (UTC+00:00)')
  })

  it('keeps the metric cards visible while the timeline is loading', async () => {
    queryClient.setQueryData(adminSummaryKey(), summary)

    const markup = markupText(await renderAppRoute('/admin/summary'))

    expect(markup).toContain('Total Indicators')
    expect(markup).toContain('Loading timeline')
  })

  it('offers the presets with 7 days selected by default', async () => {
    seedTimeline()

    const markup = markupText(await renderAppRoute('/admin/summary'))

    expect(markup).toContain('7 days')
    expect(markup).toContain('1 month')
    expect(markup).toContain('Custom')
    expect(markup).toContain('name="timeline-preset"')
    // Seeding only the 7-day query key and rendering its points proves the default preset is 7 days.
    expect(markup).toContain('2026-09-15')
  })
})
