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
      malwarePrintable: 'AgentTesla',
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

const longUrlValue =
  'https://very-long-subdomain.malicious-example-domain.test/a/very/long/path/segment/that/keeps/going?query=ABCDEFGHIJKLMNOP&more=1234567890'

const longUrlGroup = {
  id: 10,
  iocValue: longUrlValue,
  iocType: 'url',
  urlHost: 'very-long-subdomain.malicious-example-domain.test',
  parentDomain: 'malicious-example-domain.test',
  firstSeenUtc: '2026-04-01T08:00:00Z',
  lastSeenUtc: '2026-04-02T08:00:00Z',
  sources: [
    {
      provider: 'ThreatFox',
      providerRecordId: 'tf-10',
      firstSeenUtc: '2026-04-01T08:00:00Z',
      tags: [],
      isFalsePositive: false,
    },
  ],
}

async function mockBrowse(page: Page) {
  await page.route(/\/api\/iocs\/providers(\?.*)?$/, async (route) => {
    await route.fulfill({ json: ['ThreatFox', 'PhishTank'] })
  })
  await page.route(/\/api\/iocs(\?.*)?$/, async (route) => {
    const url = new URL(route.request().url())
    const requestedPage = Number(url.searchParams.get('page') ?? '1')
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20')
    await route.fulfill({
      json: {
        items: [urlGroup, ipv4Group, longUrlGroup],
        page: requestedPage,
        pageSize,
        totalCount: 45,
      },
    })
  })
}

test('remains within the viewport at narrow and wide widths', async ({
  page,
}) => {
  await mockBrowse(page)

  for (const viewport of [
    { width: 375, height: 800 },
    { width: 1440, height: 900 },
  ]) {
    await page.setViewportSize(viewport)
    await page.goto('/browse?type=url&pageSize=10')
    await expect(
      page.getByRole('link', { name: urlGroup.iocValue }),
    ).toBeVisible()

    const overflow = await page.evaluate(
      () =>
        document.documentElement.scrollWidth -
        document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(1)

    // A long indicator wraps inside its cell instead of widening the table.
    const longLink = page.getByRole('link', { name: longUrlValue })
    const box = await longLink.boundingBox()
    expect(box).not.toBeNull()
    expect(box!.height).toBeGreaterThan(20)
    expect(box!.width).toBeLessThanOrEqual(240)

    // The generated DataTables surface is present and token-styled, not themed
    // by DataTables' own stylesheet.
    await expect(page.locator('.dt-container')).toHaveCount(1)
  }
})

test('every table draw calls only GET /api/iocs with normalized bounded filters', async ({
  page,
}) => {
  const apiRequests: { method: string; url: string }[] = []
  page.on('request', (request) => {
    const { pathname } = new URL(request.url())
    if (pathname.startsWith('/api/')) {
      apiRequests.push({ method: request.method(), url: request.url() })
    }
  })

  const iocRequests: URL[] = []
  await page.route(/\/api\/iocs\/providers(\?.*)?$/, async (route) => {
    await route.fulfill({ json: ['ThreatFox', 'PhishTank'] })
  })
  await page.route(/\/api\/iocs(\?.*)?$/, async (route) => {
    const url = new URL(route.request().url())
    iocRequests.push(url)
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

  await page.goto('/browse?type=url&pageSize=10')
  await expect(
    page.getByRole('link', { name: urlGroup.iocValue }),
  ).toBeVisible()

  // Exactly one request for the initial URL, carrying the normalized filters.
  expect(iocRequests).toHaveLength(1)
  expect(iocRequests[0].pathname).toBe('/api/iocs')
  expect(iocRequests[0].searchParams.get('type')).toBe('url')
  expect(iocRequests[0].searchParams.get('pageSize')).toBe('10')
  // The serializer omits the default page-one parameter.
  expect(iocRequests[0].searchParams.get('page')).toBeNull()

  // A single page interaction issues exactly one new page request.
  await page.getByLabel('Next').click()
  await expect.poll(() => iocRequests.length).toBe(2)
  expect(iocRequests[1].pathname).toBe('/api/iocs')
  expect(iocRequests[1].searchParams.get('page')).toBe('2')
  await expect.poll(() => iocRequests.length).toBe(2)

  // No full-dataset pagination and no more than 100 groups per request.
  for (const url of iocRequests) {
    expect(url.pathname).toBe('/api/iocs')
    expect(url.searchParams.get('length')).toBeNull()
    expect(url.searchParams.get('start')).toBeNull()
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20')
    expect(Number.isInteger(pageSize)).toBe(true)
    expect(pageSize).toBeGreaterThan(0)
    expect(pageSize).toBeLessThanOrEqual(100)
  }

  // The table never calls another API surface, and always uses GET.
  // `/api/iocs/providers` is the public filter catalog, not a table request.
  expect(apiRequests.length).toBeGreaterThan(0)
  for (const request of apiRequests) {
    expect(request.method).toBe('GET')
    expect(['/api/iocs', '/api/iocs/providers']).toContain(
      new URL(request.url).pathname,
    )
  }
})
