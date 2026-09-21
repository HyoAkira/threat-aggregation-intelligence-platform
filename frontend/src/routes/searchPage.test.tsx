import { renderToStaticMarkup } from 'react-dom/server'
import {
  BulkResultItem,
  BulkResults,
  BulkSearchErrorState,
  MAX_BULK_QUERIES,
  SearchErrorState,
  validateSearchSubmission,
} from './search'
import { ApiError } from '../lib/apiErrors'
import type { BulkSearchResult } from '../lib/schemas'
import { IocGroupCard } from '../components/IocGroupCard'
import { ipv4Group, makeSession, urlGroup } from '../test/fixtures'
import { useAuthStore } from '../lib/authStore'
import {
  renderAppRoute,
  renderWithRouterContext,
  resetClientState,
} from '../test/renderApp'

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
      malware: null,
      target: null,
    })
  })

  it('normalizes multiple tags as an OR filter', () => {
    const result = validateSearchSubmission({
      q: ' 1.2.3.4 ',
      tag: ' BotNet, phishing, botnet, ',
      provider: '',
    })

    expect(result.error).toBeNull()
    expect(result.submission?.tag).toBe('botnet,phishing')
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
      malware: null,
      target: null,
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
      malware: null,
      target: null,
    })
  })

  it('submits trimmed malware and target filters', () => {
    const result = validateSearchSubmission({
      q: 'evil.example',
      tag: '',
      provider: '',
      malware: ' Emotet ',
      target: ' Amazon ',
    })
    expect(result.error).toBeNull()
    expect(result.submission?.malware).toBe('Emotet')
    expect(result.submission?.target).toBe('Amazon')
  })

  it('omits blank malware and target filters', () => {
    const result = validateSearchSubmission({
      q: 'evil.example',
      tag: '',
      provider: '',
      malware: '   ',
      target: '',
    })
    expect(result.error).toBeNull()
    expect(result.submission?.malware).toBeNull()
    expect(result.submission?.target).toBeNull()
  })

  it('rejects oversized malware and target filters without a submission', () => {
    const malware = validateSearchSubmission({
      q: 'evil.example',
      tag: '',
      provider: '',
      malware: 'a'.repeat(201),
    })
    expect(malware.error).toContain('malware')
    expect(malware.submission).toBeNull()

    const target = validateSearchSubmission({
      q: 'evil.example',
      tag: '',
      provider: '',
      target: 'a'.repeat(501),
    })
    expect(target.error).toContain('target')
    expect(target.submission).toBeNull()
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
    expect(markup).toContain('Malware filter (optional)')
    expect(markup).toContain('Target filter (optional)')
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
    // Matched source values stay visible, including the malware/target fields.
    expect(markup).toContain('win.agenttesla')
  })
})

describe('multiline submission normalization', () => {
  it('preserves order and duplicates and keeps filters for a bulk submission', () => {
    const result = validateSearchSubmission({
      q: '1.2.3.4\n\n evil.example \n1.2.3.4',
      tag: ' BotNet ',
      provider: ' 1 ',
    })

    expect(result.error).toBeNull()
    expect(result.submission).toBeNull()
    expect(result.bulk).toEqual({
      queries: ['1.2.3.4', 'evil.example', '1.2.3.4'],
      tag: 'botnet',
      provider: '1',
    })
  })

  it('keeps a single non-empty line on the existing exact-search path', () => {
    const result = validateSearchSubmission({
      q: ' 1.2.3.4 \n  \n',
      tag: '',
      provider: '',
    })

    expect(result.bulk).toBeNull()
    expect(result.submission).toEqual({
      q: '1.2.3.4',
      tag: null,
      provider: null,
      malware: null,
      target: null,
    })
  })

  it('rejects an editor that contains only blank lines', () => {
    expect(
      validateSearchSubmission({ q: '\n   \n', tag: '', provider: '' }).error,
    ).toContain('Enter an indicator')
  })

  it('rejects more values than the bulk contract allows', () => {
    const q = Array.from(
      { length: MAX_BULK_QUERIES + 1 },
      (_, index) => `host${index}.example`,
    ).join('\n')

    expect(
      validateSearchSubmission({ q, tag: '', provider: '' }).error,
    ).toContain('100')
  })
})

describe('bulk result presentation', () => {
  const matched = {
    input: '1.2.3.4',
    status: 'matched' as const,
    groups: [ipv4Group],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  }
  const notFound = {
    input: 'absent.example',
    status: 'not_found' as const,
    groups: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    error: { code: 'ioc_not_found', detail: 'No domain IOC matches.' },
  }
  const invalid = {
    input: 'not an ioc',
    status: 'invalid' as const,
    groups: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    error: { code: 'invalid_input', detail: 'Not a supported IOC.' },
  }
  const mixed: BulkSearchResult = {
    items: [matched, notFound, invalid],
    totalCount: 3,
    matchedCount: 1,
    notFoundCount: 1,
    invalidCount: 1,
  }

  it('renders the summary and one section per input in submission order', async () => {
    const markup = await renderWithRouterContext(
      <BulkResults data={mixed} page={1} onPage={() => {}} />,
    )
    const text = markup.replace(/<!--.*?-->/g, '')

    expect(text).toContain('Results for 3 indicators')
    expect(text).toContain('1 matched · 1 not found · 1 invalid')

    const matchedIndex = text.indexOf('1.2.3.4')
    const notFoundIndex = text.indexOf('absent.example')
    const invalidIndex = text.indexOf('not an ioc')
    expect(matchedIndex).toBeGreaterThan(-1)
    expect(notFoundIndex).toBeGreaterThan(matchedIndex)
    expect(invalidIndex).toBeGreaterThan(notFoundIndex)
  })

  it('renders matched groups but no false result card for invalid or absent values', async () => {
    const markup = await renderWithRouterContext(
      <BulkResults data={mixed} page={1} onPage={() => {}} />,
    )
    const text = markup.replace(/<!--.*?-->/g, '')

    expect(text.match(/IOC group /g)?.length).toBe(1)
    expect(text).toContain('Not found')
    expect(text).toContain('No domain IOC matches.')
    expect(text).toContain('Invalid')
    expect(text).toContain('Not a supported IOC.')
  })

  it('shows shared page navigation only when a matched value has more pages', async () => {
    const single = (
      await renderWithRouterContext(
        <BulkResults data={mixed} page={1} onPage={() => {}} />,
      )
    ).replace(/<!--.*?-->/g, '')
    expect(single).not.toContain('Search page')

    const paged = (
      await renderWithRouterContext(
        <BulkResults
          data={{
            items: [{ ...matched, totalCount: 45 }],
            totalCount: 1,
            matchedCount: 1,
            notFoundCount: 0,
            invalidCount: 0,
          }}
          page={2}
          onPage={() => {}}
        />,
      )
    ).replace(/<!--.*?-->/g, '')
    expect(paged).toContain('Search page 2 of 3')
  })

  it('a single bulk item renders its status explanation without a group card', () => {
    const markup = renderToStaticMarkup(<BulkResultItem item={invalid} />)
    expect(markup).toContain('Invalid')
    expect(markup).not.toContain('IOC group ')
  })
})

describe('bulk request error classification', () => {
  it('a transport failure is retryable and shows the correlation id', () => {
    const markup = renderToStaticMarkup(
      <BulkSearchErrorState
        error={
          new ApiError(500, 'server_error', 'Unexpected failure.', 'corr-bulk')
        }
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Search failed')
    expect(markup).toContain('corr-bulk')
    expect(markup).toContain('Retry')
  })

  it('a 403 shows the forbidden state without retry', () => {
    const markup = renderToStaticMarkup(
      <BulkSearchErrorState
        error={new ApiError(403, 'forbidden', 'no')}
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })
})

describe('multiline search page render', () => {
  beforeEach(() => {
    resetClientState()
    useAuthStore.setState({ session: null })
  })
  afterEach(() => resetClientState())

  it('renders the multiline editor and guidance without a type selector', async () => {
    const markup = await renderAppRoute('/search')

    expect(markup).toContain('<textarea')
    expect(markup).toContain('Enter one IOC per line')
    expect(markup).not.toContain('name="type"')
    expect(markup).not.toContain('Indicator type')
  })
})
