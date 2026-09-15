import { renderToStaticMarkup } from 'react-dom/server'
import { SearchErrorState, validateSearchSubmission } from './search'
import { ApiError } from '../lib/apiErrors'
import { IocGroupCard } from '../components/IocGroupCard'
import { makeSession, urlGroup } from '../test/fixtures'
import { useAuthStore } from '../lib/authStore'
import { renderAppRoute, resetClientState } from '../test/renderApp'

describe('validateSearchSubmission (local rejection of empty input)', () => {
  it('rejects an empty or whitespace-only indicator', () => {
    expect(
      validateSearchSubmission({ q: '', tag: '', provider: '' }).error,
    ).toBe('Enter an indicator to search.')
    expect(
      validateSearchSubmission({ q: '   ', tag: '', provider: '' }).error,
    ).toBe('Enter an indicator to search.')
  })

  it('trims the indicator and optional filters on a valid submission', () => {
    const result = validateSearchSubmission({
      q: '  1.2.3.4  ',
      tag: '  botnet ',
      provider: ' 1 ',
    })
    expect(result.error).toBeNull()
    expect(result.submission).toEqual({
      q: '1.2.3.4',
      tag: 'botnet',
      provider: '1',
    })
  })

  it('treats blank optional filters as absent', () => {
    const result = validateSearchSubmission({
      q: 'evil.example',
      tag: '',
      provider: '',
    })
    expect(result.submission).toEqual({
      q: 'evil.example',
      tag: null,
      provider: null,
    })
  })

  it('accepts a free-form provider name filter', () => {
    const result = validateSearchSubmission({
      q: 'x',
      tag: '',
      provider: 'ThreatFox',
    })
    expect(result.error).toBeNull()
    expect(result.submission).toEqual({
      q: 'x',
      tag: null,
      provider: 'ThreatFox',
    })
  })

  it('rejects oversized input at the frontend boundary', () => {
    expect(
      validateSearchSubmission({ q: 'a'.repeat(2049), tag: '', provider: '' })
        .error,
    ).toContain('long')
    expect(
      validateSearchSubmission({ q: 'a', tag: 'b'.repeat(101), provider: '' })
        .error,
    ).toContain('tag')
  })
})

describe('SearchErrorState keeps 400 and 404 distinct', () => {
  const notFound = new ApiError(404, 'not_found', 'absent', 'corr-a', {
    code: 'not_found',
    title: 'Not Found',
    status: 404,
    detail: 'No stored indicator matches this value.',
    correlationId: 'corr-a',
  })
  const invalid = new ApiError(
    400,
    'invalid_input',
    'The query could not be classified as a supported indicator type.',
    'corr-b',
    {
      code: 'invalid_input',
      title: 'Invalid input',
      status: 400,
      detail:
        'The query could not be classified as a supported indicator type.',
      correlationId: 'corr-b',
    },
  )
  const serverError = new ApiError(
    500,
    'server_error',
    'Unexpected failure.',
    'corr-c',
  )
  const forbidden = new ApiError(403, 'forbidden', 'no', 'corr-d')

  it('a 400 explains invalid syntax using the API reason', () => {
    const markup = renderToStaticMarkup(
      <SearchErrorState error={invalid} q="not an ioc!" onRetry={() => {}} />,
    )
    expect(markup).toContain('Invalid indicator')
    expect(markup).toContain(
      'The query could not be classified as a supported indicator type.',
    )
    expect(markup).not.toContain('No indicator found')
  })

  it('a 400 without a problem body falls back to the local explanation', () => {
    const bare = new ApiError(400, 'invalid_input', 'Bad request')
    const markup = renderToStaticMarkup(
      <SearchErrorState error={bare} q="not an ioc!" onRetry={() => {}} />,
    )
    expect(markup).toContain('Invalid indicator')
    expect(markup).toContain(
      'not a supported IPv4, IPv6, domain, or HTTP(S) URL',
    )
  })

  it('a 404 explains a valid indicator that is absent', () => {
    const markup = renderToStaticMarkup(
      <SearchErrorState
        error={notFound}
        q="absent.example"
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('No indicator found')
    expect(markup).toContain('absent.example')
    expect(markup).not.toContain('Invalid indicator')
  })

  it('an unexpected failure is retryable and shows the correlation id', () => {
    const markup = renderToStaticMarkup(
      <SearchErrorState
        error={serverError}
        q="evil.example"
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Search failed')
    expect(markup).toContain('corr-c')
    expect(markup).toContain('Retry')
  })

  it('a 403 shows the forbidden state without retry', () => {
    const markup = renderToStaticMarkup(
      <SearchErrorState
        error={forbidden}
        q="evil.example"
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })
})

describe('search page render', () => {
  beforeEach(() => {
    resetClientState()
    useAuthStore.setState({ session: null })
  })
  afterEach(() => resetClientState())

  it('renders the form with one required indicator input and no type selector', async () => {
    const markup = await renderAppRoute('/search')

    expect(markup).toContain('Search indicators')
    expect(markup).toContain('Indicator (IPv4, IPv6, domain, or HTTP(S) URL)')
    expect(markup).toContain('Tag filter (optional)')
    expect(markup).toContain('Provider filter (optional)')
    // The user never chooses an IOC type: no type control exists.
    expect(markup).not.toContain('name="type"')
    expect(markup).not.toContain('Indicator type')
  })

  it('keeps the authenticated shell around the public search page', async () => {
    useAuthStore.setState({ session: makeSession({ role: 'User' }) })
    const markup = await renderAppRoute('/search')
    expect(markup).toContain('Search indicators')
  })
})

describe('grouped success presentation', () => {
  it('renders one group summary with every source as a separate labelled entry', () => {
    // Two providers and a port variant stay separate under the shared group card.
    const group = {
      ...urlGroup,
      sources: [
        ...urlGroup.sources,
        { ...urlGroup.sources[0], providerRecordId: 'tf-17b', port: 8443 },
      ],
    }
    const markup = renderToStaticMarkup(<IocGroupCard group={group} />)

    expect(markup).toContain('IOC group https://evil.example/x')
    expect(markup).toContain('Source: ThreatFox')
    expect(markup).toContain('Source: PhishTank')
    expect(markup).toContain('record tf-17b')
    expect(markup).toContain('8443')
    expect(markup).toContain('First seen:')
    expect(markup).toContain('Last seen:')
  })
})
