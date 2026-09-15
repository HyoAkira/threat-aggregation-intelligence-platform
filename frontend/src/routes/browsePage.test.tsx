import { renderToStaticMarkup } from 'react-dom/server'
import { queryClient } from '../lib/queryClient'
import { browseKey } from '../lib/queryKeys'
import { ApiError } from '../lib/apiErrors'
import { BrowseErrorState } from './browse'
import {
  browseParamsToSearch,
  defaultBrowseParams,
  parseBrowseParams,
} from '../lib/browseParams'
import type { BrowseParams } from '../lib/browseParams'
import { renderAppRoute, resetClientState } from '../test/renderApp'
import { makePaged, urlGroup, ipv4Group } from '../test/fixtures'

describe('browse page render', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('renders the default view with controls and one page of results', async () => {
    queryClient.setQueryData(
      browseKey(defaultBrowseParams),
      makePaged([urlGroup, ipv4Group], 1, 20, 45),
    )

    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('Browse indicators')
    expect(markup).toContain(urlGroup.iocValue)
    expect(markup).toContain(ipv4Group.iocValue)
    expect(markup).toContain('Threat family / campaign')
    expect(markup).toMatch(/<!-- -->1<!-- --> of <!-- -->3/)
    expect(markup).toMatch(/<!-- -->45<!-- --> total/)
    expect(markup).toContain('Previous page')
    expect(markup).toContain('Next page')
  })

  it('restores an active view from the URL (reload, Back, Forward, shared links)', async () => {
    const url =
      '/browse?type=url&provider=1&tag=phishing&orderBy=iocValue&dir=asc&page=2&pageSize=10'
    const parsed = parseBrowseParams({
      // The router coerces numeric params to strings before validateSearch output.
      type: 'url',
      provider: '1',
      tag: 'phishing',
      orderBy: 'iocValue',
      dir: 'asc',
      page: '2',
      pageSize: '10',
    })
    expect(parsed.issues).toEqual([])
    if (!parsed.params)
      throw new Error('expected the URL to parse into browse params')
    queryClient.setQueryData(
      browseKey(parsed.params),
      makePaged([urlGroup], 2, 10, 45),
    )

    const markup = await renderAppRoute(url)

    // Every control reflects the URL state.
    expect(markup).toMatch(/aria-pressed="true"[^>]*>URL<\/button>/)
    expect(markup).toContain('<option value="iocValue" selected="">')
    expect(markup).toContain('<option value="asc" selected="">')
    expect(markup).toContain('<option value="10" selected="">')
    expect(markup).toContain('value="phishing"')
    expect(markup).toContain('value="1"')
    // The seeded page-two data renders.
    expect(markup).toContain(urlGroup.iocValue)
    expect(markup).toMatch(/<!-- -->2<!-- --> of <!-- -->5/)
    expect(markup).not.toContain(ipv4Group.iocValue)
  })

  it('distinguishes empty data from a request failure', async () => {
    const filtered = { ...defaultBrowseParams, type: 'url' as const }
    queryClient.setQueryData(browseKey(filtered), makePaged([], 1, 20, 0))

    const emptyMarkup = await renderAppRoute('/browse?type=url')

    expect(emptyMarkup).toContain('No indicators found')
    expect(emptyMarkup).toContain('role="status"')
    expect(emptyMarkup).not.toContain('role="alert"')
    expect(emptyMarkup).not.toContain('Loading indicators')
    // With filters active, the empty state offers to clear them.
    expect(emptyMarkup).toContain('Clear filters')
  })

  it('empty data with no active filters offers no clear-filters action', async () => {
    queryClient.setQueryData(
      browseKey(defaultBrowseParams),
      makePaged([], 1, 20, 0),
    )
    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('No indicators found')
    expect(markup).not.toContain('Clear filters')
  })

  it('renders the pending state while results load', async () => {
    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('Loading indicators')
    expect(markup).toContain('Indicator type')
  })

  it('shows an invalid-filters state with a reset affordance for unsupported URL values', async () => {
    const markup = await renderAppRoute('/browse?type=email')

    expect(markup).toContain('Unsupported filters')
    expect(markup).toContain('email')
    expect(markup).toContain('Reset to the default view')
    // No request ran for invalid filters.
    expect(markup).not.toContain('Loading indicators')
  })

  it('exposes every control the E09 browse contract supports', async () => {
    const markup = await renderAppRoute('/browse')

    expect(markup).toContain('Indicator type')
    expect(markup).toContain('Tag')
    expect(markup).toContain('Provider')
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
