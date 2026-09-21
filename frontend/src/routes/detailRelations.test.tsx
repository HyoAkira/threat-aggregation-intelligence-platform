import { renderToStaticMarkup } from 'react-dom/server'
import { queryClient } from '../lib/queryClient'
import { iocDetailKey, iocRelationsKey } from '../lib/queryKeys'
import { ApiError } from '../lib/apiErrors'
import { DetailErrorState, RelationsErrorState } from './iocs.$id'
import { renderAppRoute, resetClientState } from '../test/renderApp'
import { ipv4Group, makePaged, urlGroup } from '../test/fixtures'

function seedDetail(group: typeof urlGroup) {
  queryClient.setQueryData(iocDetailKey(group.id), group)
}

function seedRelations(
  id: number,
  relation: 'urlHost' | 'parentDomain' | 'malware' | 'target',
  page: number,
  data: ReturnType<typeof makePaged>,
) {
  queryClient.setQueryData(iocRelationsKey(id, relation, page), data)
}

describe('IOC detail page', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('renders the group value, type, aggregate dates, and every provider source', async () => {
    seedDetail(urlGroup)

    const markup = await renderAppRoute('/iocs/7')

    expect(markup).toContain('https://evil.example/x')
    expect(markup).toContain('url')
    expect(markup).toContain('First seen:')
    expect(markup).toContain('2026-01-01 08:00:00 UTC')
    expect(markup).toContain('Last seen:')
    expect(markup).toContain('2026-07-01 08:00:00 UTC')
    expect(markup).toContain('Source: ThreatFox')
    expect(markup).toContain('Source: PhishTank')
    // Application-derived status moved to the IOC header badge.
    expect(markup).toContain('1 source marked false positive')
  })

  it('rejects a malformed id locally without calling the API', async () => {
    const markup = await renderAppRoute('/iocs/not-a-number')

    expect(markup).toContain('Invalid indicator id')
  })

  it('renders the pending state while the detail request runs', async () => {
    const markup = await renderAppRoute('/iocs/7')

    expect(markup).toContain('Loading indicator')
  })
})

describe('detail error classification', () => {
  it('a 404 shows a distinct not-found state', () => {
    const markup = renderToStaticMarkup(
      <DetailErrorState
        error={
          new ApiError(404, 'not_found', 'No row with id 999.', 'corr-404')
        }
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Indicator not found')
    expect(markup).toContain('may have been removed')
    expect(markup).not.toContain('Retry')
  })

  it('a 403 shows the forbidden state without retry', () => {
    const markup = renderToStaticMarkup(
      <DetailErrorState
        error={new ApiError(403, 'forbidden', 'Admin only.')}
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })

  it('an unexpected failure is retryable and shows the correlation id', () => {
    const markup = renderToStaticMarkup(
      <DetailErrorState
        error={
          new ApiError(500, 'server_error', 'Unexpected failure.', 'corr-500')
        }
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Could not load indicator')
    expect(markup).toContain('Unexpected failure.')
    expect(markup).toContain('corr-500')
    expect(markup).toContain('Retry')
  })

  it('a 403 on relations shows the forbidden state without infinite retry', () => {
    const markup = renderToStaticMarkup(
      <RelationsErrorState
        error={new ApiError(403, 'forbidden', 'Admin only.')}
        onRetry={() => {}}
      />,
    )
    expect(markup).toContain('Access denied')
    expect(markup).not.toContain('Retry')
  })
})

describe('on-demand relations', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('offers host and parent-domain relations for a URL group, loading only on request', async () => {
    seedDetail(urlGroup)

    const markup = await renderAppRoute('/iocs/7')

    expect(markup).toContain('Related indicators')
    expect(markup).toContain('URLs on host evil.example')
    expect(markup).toContain('Indicators under evil.example')
    // Without a relation requested, no relation data is fetched or rendered.
    expect(markup).not.toContain('Relations page')
  })

  it('renders one bounded page at a time with next and previous states', async () => {
    seedDetail(urlGroup)
    seedRelations(7, 'urlHost', 1, makePaged([urlGroup, ipv4Group], 1, 20, 45))

    const markup = await renderAppRoute('/iocs/7?relation=urlHost')

    expect(markup).toMatch(
      /Relations page<!-- --> <!-- -->1<!-- --> of <!-- -->3/,
    )
    expect(markup).toMatch(/<!-- -->45<!-- --> total/)
    expect(markup).toContain('Previous page')
    expect(markup).toContain('Next page')
    expect(markup).toContain('IOC group https://evil.example/x')
  })

  it('requests the selected page from the API by URL state', async () => {
    seedDetail(urlGroup)
    seedRelations(7, 'urlHost', 2, makePaged([ipv4Group], 2, 20, 45))

    const markup = await renderAppRoute('/iocs/7?relation=urlHost&relPage=2')

    expect(markup).toMatch(
      /Relations page<!-- --> <!-- -->2<!-- --> of <!-- -->3/,
    )
    expect(markup).toContain('IOC group 1.2.3.4')
  })

  it('shows the empty state when a valid group has no relations', async () => {
    seedDetail(urlGroup)
    seedRelations(7, 'parentDomain', 1, makePaged([], 1, 20, 0))

    const markup = await renderAppRoute('/iocs/7?relation=parentDomain')

    expect(markup).toContain('No related indicators')
    expect(markup).not.toContain('Relations page')
  })

  it('IP details do not offer host relations at all', async () => {
    seedDetail(ipv4Group)

    const markup = await renderAppRoute('/iocs/8')

    expect(markup).not.toContain('Related indicators')
    expect(markup).not.toContain('URLs on host')
    expect(markup).not.toContain('Indicators under')
  })

  it('renders the relations pending state while a requested page loads', async () => {
    seedDetail(urlGroup)

    const markup = await renderAppRoute('/iocs/7?relation=urlHost')

    expect(markup).toContain('Loading related indicators')
  })

  it('offers a malware relation when a source carries a malware value', async () => {
    seedDetail(urlGroup)

    const markup = await renderAppRoute('/iocs/7')

    expect(markup).toContain('Indicators sharing this malware')
    // The URL group's sources carry no target, so its control is hidden.
    expect(markup).not.toContain('Indicators sharing this target')
  })

  it('a later source enables a metadata relation', async () => {
    const group = {
      ...urlGroup,
      id: 21,
      sources: [
        { ...urlGroup.sources[0], malware: null },
        { ...urlGroup.sources[1], malware: null, target: 'Amazon' },
      ],
    }
    seedDetail(group)

    const markup = await renderAppRoute('/iocs/21')

    expect(markup).toContain('Indicators sharing this target')
    expect(markup).not.toContain('Indicators sharing this malware')
  })

  it('hides metadata relation controls when no source carries a value', async () => {
    seedDetail(ipv4Group)

    const markup = await renderAppRoute('/iocs/8')

    expect(markup).not.toContain('Indicators sharing this malware')
    expect(markup).not.toContain('Indicators sharing this target')
  })

  it('offers a target relation for an IP detail and loads its page', async () => {
    const group = {
      ...ipv4Group,
      id: 22,
      sources: [{ ...ipv4Group.sources[0], target: 'Amazon' }],
    }
    seedDetail(group)
    seedRelations(22, 'target', 1, makePaged([urlGroup], 1, 20, 1))

    const markup = await renderAppRoute('/iocs/22?relation=target')

    expect(markup).toContain('Indicators sharing this target')
    expect(markup).toMatch(/Relations page<!-- --> <!-- -->1<!-- --> of <!-- -->1/)
    expect(markup).toContain('IOC group https://evil.example/x')
  })
})
