import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'

const urlGroup = {
  id: 7,
  iocValue: 'https://evil.example/x',
  iocType: 'url',
  urlHost: 'evil.example',
  parentDomain: 'evil.example',
  firstSeenUtc: '2026-01-01T08:00:00Z',
  lastSeenUtc: '2026-07-01T08:00:00Z',
  sources: [
    {
      provider: 'ThreatFox',
      providerRecordId: 'tf-7',
      firstSeenUtc: '2026-01-01T08:00:00Z',
      tags: ['botnet'],
      isFalsePositive: false,
    },
  ],
}

const ipv4Group = {
  id: 8,
  iocValue: '1.2.3.4',
  iocType: 'ipv4',
  firstSeenUtc: '2026-01-02T08:00:00Z',
  lastSeenUtc: '2026-05-02T08:00:00Z',
  sources: [
    {
      provider: 'ThreatFox',
      providerRecordId: 'tf-8',
      firstSeenUtc: '2026-01-02T08:00:00Z',
      tags: [],
      isFalsePositive: false,
    },
  ],
}

/** Mocks the public browse endpoints and records every browse-table request. */
async function mockPublicIocs(
  page: Page,
  onBrowse?: (url: URL) => void,
): Promise<void> {
  await page.route('**/api/**', (route) => route.fulfill({ json: [] }))
  await page.route(/\/api\/iocs\/providers(\?.*)?$/, (route) =>
    route.fulfill({ json: ['ThreatFox', 'PhishTank'] }),
  )
  await page.route(/\/api\/iocs(\?.*)?$/, async (route) => {
    const url = new URL(route.request().url())
    onBrowse?.(url)
    const requestedPage = Number(url.searchParams.get('page') ?? '1')
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20')
    await route.fulfill({
      json: {
        items: [urlGroup, ipv4Group],
        page: requestedPage,
        pageSize,
        totalCount: 45,
      },
    })
  })
}

test('opens /browse?type=url&pageSize=10 directly against the static preview', async ({
  page,
}) => {
  const requests: URL[] = []
  await mockPublicIocs(page, (url) => requests.push(url))

  await page.goto('/browse?type=url&pageSize=10')

  // The router resolves the deep link and preserves the requested search state.
  const initial = new URL(page.url())
  expect(initial.pathname).toBe('/browse')
  expect(initial.searchParams.get('type')).toBe('url')
  expect(initial.searchParams.get('pageSize')).toBe('10')

  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()

  // The existing normalized request is preserved through the static runtime.
  expect(requests).toHaveLength(1)
  expect(requests[0].pathname).toBe('/api/iocs')
  expect(requests[0].searchParams.get('type')).toBe('url')
  expect(requests[0].searchParams.get('pageSize')).toBe('10')
  expect(requests[0].searchParams.get('page')).toBeNull()
})

test('preserves in-app navigation and back/forward URL state', async ({
  page,
}) => {
  await mockPublicIocs(page)

  await page.goto('/')
  await page.getByRole('link', { name: 'Browse indicators' }).click()
  await expect(page).toHaveURL(/\/browse$/)
  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()

  // A pagination interaction updates the URL through client-side navigation.
  // The router's default search serializer JSON-encodes the page value.
  await page.getByLabel('Next').click()
  await expect(page).toHaveURL(/[?&]page=(%22)?2(%22)?(&|$)/)
  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()

  // Back and forward restore the same rendered view from the URL alone.
  await page.goBack()
  await expect(page).toHaveURL(/\/browse$/)
  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()

  await page.goForward()
  await expect(page).toHaveURL(/[?&]page=(%22)?2(%22)?(&|$)/)
  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()
})

const providerDashboardItem = {
  id: 1,
  name: 'ThreatFox',
  baseUrl: 'https://threatfox.abuse.ch',
  isEnabled: true,
  intervalMinutes: 60,
  lastFetchStatus: 'success',
  lastFetchAt: '2026-09-16T00:00:00Z',
  nextFetchTime: '2026-09-16T01:00:00Z',
  supportedModes: ['recent', 'full'],
  defaultMode: 'recent',
  recentCooldownMinutes: 5,
  fullCooldownMinutes: 60,
  minimumIntervalMinutes: 15,
}

const adminSession = {
  token: 'e2e-admin-token',
  username: 'admin',
  role: 'Admin' as const,
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
}

test('a pre-seeded Admin session opens /admin/providers directly with bearer auth', async ({
  page,
}) => {
  let authorization: string | undefined
  await page.route(/\/api\/admin\/providers(\?.*)?$/, async (route) => {
    authorization = route.request().headers()['authorization']
    await route.fulfill({ json: [providerDashboardItem] })
  })

  // Seed the session before the application starts so the client-side guard
  // sees it during the first render.
  await page.addInitScript(
    ({ key, value }) => {
      window.sessionStorage.setItem(key, value)
    },
    { key: 'ti.session', value: JSON.stringify(adminSession) },
  )

  await page.goto('/admin/providers')

  await expect(
    page.getByRole('heading', { name: 'Admin Console' }),
  ).toBeVisible()
  await expect(page.getByText('ThreatFox', { exact: true })).toBeVisible()
  expect(authorization).toBe(`Bearer ${adminSession.token}`)
})

test('an absent session redirects /admin/providers through login without Admin data', async ({
  page,
}) => {
  let adminDataRequested = false
  await page.route(/\/api\/admin\/providers(\?.*)?$/, async (route) => {
    adminDataRequested = true
    await route.fulfill({ json: [providerDashboardItem] })
  })

  await page.goto('/admin/providers')

  await expect(page).toHaveURL(/\/login\?.*redirect=/)
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
  expect(adminDataRequested).toBe(false)
  await expect(page.getByText('Admin Console')).toHaveCount(0)
  await expect(page.getByText('ThreatFox', { exact: true })).toHaveCount(0)
})
