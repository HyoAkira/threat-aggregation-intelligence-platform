import { detectIocType } from './index'
import { queryClient } from '../lib/queryClient'
import { browseKey } from '../lib/queryKeys'
import { defaultBrowseParams } from '../lib/browseParams'
import { renderAppRoute, resetClientState } from '../test/renderApp'
import { ipv4Group, makePaged, urlGroup } from '../test/fixtures'

describe('detectIocType (search bar hint only; the API owns classification)', () => {
  it('detects each supported indicator type', () => {
    expect(detectIocType('194.26.29.112')).toBe('ipv4')
    expect(detectIocType('2001:0db8:85a3:0000:0000:8a2e:0370:7334')).toBe(
      'ipv6',
    )
    expect(detectIocType('::1')).toBe('ipv6')
    expect(detectIocType('https://cdn-payloads.org/v2/loader.bin')).toBe('url')
    expect(detectIocType('cobaltstrike-c2.top')).toBe('domain')
  })

  it('returns no hint for empty or unsupported values', () => {
    expect(detectIocType('')).toBeNull()
    expect(detectIocType('   ')).toBeNull()
    expect(detectIocType('d41d8cd98f00b204e9800998ecf8427e')).toBeNull()
    expect(detectIocType('free-text keyword')).toBeNull()
  })
})

/** renderToString splits adjacent text/expression nodes with comment markers. */
function markupText(markup: string): string {
  return markup.replaceAll('<!-- -->', '')
}

describe('home page render', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('renders the hub hero and search form for anonymous visitors', async () => {
    const markup = markupText(await renderAppRoute('/'))

    expect(markup).toContain('Inspect Any Indicator of Compromise.')
    expect(markup).toContain('Indicator (IPv4, IPv6, domain, or HTTP(S) URL)')
    expect(markup).toContain('Auto-detect')
    expect(markup).toContain('Quick queries')
    expect(markup).not.toContain('Sign-in required')
  })

  it('renders the recent ingest table with source attribution from real browse data', async () => {
    queryClient.setQueryData(
      browseKey({ ...defaultBrowseParams, pageSize: 10 }),
      makePaged([ipv4Group, urlGroup], 1, 10, 45),
    )

    const markup = markupText(await renderAppRoute('/'))

    expect(markup).toContain('Recently ingested indicators')
    expect(markup).toContain(ipv4Group.iocValue)
    expect(markup).toContain(urlGroup.iocValue)
    expect(markup).toContain('ThreatFox')
    expect(markup).toContain('AgentTesla')
    expect(markup).toContain('Showing 2 of 45 stored indicators')
    expect(markup).toContain('Open full explorer')
  })

  it('renders per-type totals and their share of all stored indicators', async () => {
    queryClient.setQueryData(
      browseKey({ ...defaultBrowseParams, pageSize: 1 }),
      makePaged([], 1, 1, 200),
    )
    queryClient.setQueryData(
      browseKey({ ...defaultBrowseParams, type: 'ipv4', pageSize: 1 }),
      makePaged([], 1, 1, 148),
    )

    const markup = markupText(await renderAppRoute('/'))

    expect(markup).toContain('IPv4 indicators')
    expect(markup).toContain('148')
    expect(markup).toContain('74% of all stored indicators')
  })
})
