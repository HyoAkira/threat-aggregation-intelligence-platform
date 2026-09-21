import { renderToStaticMarkup } from 'react-dom/server'
import { ApiError } from '../lib/apiErrors'
import { BrowseErrorState } from './browse'
import {
  browseParamsToSearch,
  defaultBrowseParams,
  parseBrowseParams,
} from '../lib/browseParams'
import type { BrowseParams } from '../lib/browseParams'
import { renderAppRoute } from '../test/renderApp'

describe('browse page server render', () => {
  it('renders the shell and an accessible client-only fallback without touching the DOM', async () => {
    // The Node environment has no `window`/`document`; a successful render
    // proves the browse route keeps browser-only DataTables code isolated.
    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('Browse indicators')
    expect(markup).toContain('Filter Telemetry')
    expect(markup).toContain('Apply filters')
    expect(markup).toContain('Loading indicators')
    expect(markup).toContain('Preparing the indicator table')
    expect(markup).toContain('role="status"')
  })

  it('renders the deterministic fallback instead of server-rendered rows', async () => {
    const markup = await renderAppRoute('/browse')

    // The DataTables surface is client-only, so no IOC rows are in the SSR HTML.
    expect(markup).not.toContain('https://evil.example/x')
    expect(markup).not.toContain('<tbody')
  })

  it('restores active controls from the URL (reload, Back, Forward, shared links)', async () => {
    const url =
      '/browse?type=url&provider=1&tag=phishing&malware=Emotet&target=Amazon&orderBy=iocValue&dir=asc&page=2&pageSize=10'
    const markup = await renderAppRoute(url)

    // Every control reflects the URL state.
    expect(markup).toMatch(/aria-pressed="true"[^>]*>URL<\/button>/)
    expect(markup).toContain('<option value="iocValue" selected="">')
    expect(markup).toContain('<option value="asc" selected="">')
    expect(markup).toContain('<option value="10" selected="">')
    expect(markup).toContain('value="phishing"')
    expect(markup).toContain('value="Emotet"')
    expect(markup).toContain('value="Amazon"')
    expect(markup).toContain('id="browse-provider" multiple=""')
  })

  it('shows an invalid-filters state for an oversized malware or target URL value', async () => {
    const markup = await renderAppRoute(
      `/browse?malware=${'a'.repeat(201)}`,
    )

    expect(markup).toContain('Unsupported filters')
    expect(markup).toContain('malware')
    expect(markup).not.toContain('Loading indicators')
    expect(markup).not.toContain('Preparing the indicator table')
  })

  it('shows an invalid-filters state with a reset affordance for unsupported URL values', async () => {
    const markup = await renderAppRoute('/browse?type=email')

    expect(markup).toContain('Unsupported filters')
    expect(markup).toContain('email')
    expect(markup).toContain('Reset to the default view')
    // No table is mounted and no pending state is shown for invalid filters.
    expect(markup).not.toContain('Loading indicators')
    expect(markup).not.toContain('Preparing the indicator table')
  })

  it('exposes every control the E09 browse contract supports', async () => {
    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('Indicator type')
    expect(markup).toContain('Tag')
    expect(markup).toContain('Provider')
    expect(markup).toContain('Malware')
    expect(markup).toContain('Target')
    expect(markup).toContain('Order by')
    expect(markup).toContain('Direction')
    expect(markup).toContain('Page size')
    expect(markup).toContain('Observed period')
    expect(markup).toContain('Apply filters')
  })
})

describe('browse error classification', () => {
  it('an unexpected failure is retryable and shows the correlation id', () => {
    const markup = renderToStaticMarkup(
      <BrowseErrorState
        error={
          new ApiError(500, 'server_error', 'Unexpected failure.', 'corr-b')
        }
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Could not load indicators')
    expect(markup).toContain('corr-b')
    expect(markup).toContain('Retry')
  })

  it('a 403 shows the forbidden state without retry', () => {
    const markup = renderToStaticMarkup(
      <BrowseErrorState
        error={new ApiError(403, 'forbidden', 'Admin only.')}
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })
})

describe('URL round-trip guarantees Back/Forward and reload semantics', () => {
  it('a serialized view parses back to the exact same state', () => {
    const params: BrowseParams = {
      type: 'domain',
      provider: '3',
      tag: 'botnet',
      malware: 'Emotet',
      target: 'Amazon',
      lastSeenFrom: '2026-01-01T00:00:00.000Z',
      lastSeenTo: '2026-01-31T23:59:59.000Z',
      orderBy: 'firstSeenUtc',
      dir: 'asc',
      page: 4,
      pageSize: 50,
    }
    const search = browseParamsToSearch(params)
    const { params: restored, issues } = parseBrowseParams(search)

    expect(issues).toEqual([])
    expect(restored).toEqual(params)
  })

  it('filter changes reset to page 1 by construction (page omitted at default)', () => {
    const changed: BrowseParams = {
      ...defaultBrowseParams,
      type: 'url',
      page: 1,
    }
    expect(browseParamsToSearch(changed).page).toBeUndefined()
  })
})
