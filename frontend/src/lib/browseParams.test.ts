import {
  BROWSE_PAGE_SIZE_MAX,
  browseParamsToSearch,
  defaultBrowseParams,
  hasActiveBrowseFilters,
  parseBrowseParams,
} from './browseParams'

describe('parseBrowseParams', () => {
  it('yields the default view for absent or empty values', () => {
    const { params, issues } = parseBrowseParams({
      type: '',
      provider: '  ',
      tag: undefined,
      orderBy: '',
      dir: '',
      page: '',
      pageSize: '',
    })

    expect(issues).toEqual([])
    expect(params).toEqual(defaultBrowseParams)
  })

  it('accepts every supported control value', () => {
    const { params, issues } = parseBrowseParams({
      type: 'url',
      provider: '1',
      tag: ' phishing ',
      orderBy: 'iocValue',
      dir: 'asc',
      page: '3',
      pageSize: '50',
    })

    expect(issues).toEqual([])
    expect(params).toEqual({
      type: 'url',
      provider: 1,
      tag: 'phishing',
      lastSeenFrom: null,
      lastSeenTo: null,
      orderBy: 'iocValue',
      dir: 'asc',
      page: 3,
      pageSize: 50,
    })
  })

  it('rejects an unsupported indicator type', () => {
    const { params, issues } = parseBrowseParams({ type: 'email' })
    expect(params).toBeNull()
    expect(issues[0]).toContain('email')
  })

  it('rejects a non-positive provider id', () => {
    const { params, issues } = parseBrowseParams({ provider: 'abc' })
    expect(params).toBeNull()
    expect(issues.length).toBe(1)

    expect(parseBrowseParams({ provider: '0' }).params).toBeNull()
  })

  it('rejects an unsupported sort field and direction', () => {
    expect(parseBrowseParams({ orderBy: 'providerRecordId' }).params).toBeNull()
    expect(parseBrowseParams({ dir: 'sideways' }).params).toBeNull()
  })

  it('rejects out-of-bounds page and page size', () => {
    expect(parseBrowseParams({ page: '0' }).params).toBeNull()
    expect(parseBrowseParams({ page: '-2' }).params).toBeNull()
    expect(
      parseBrowseParams({ pageSize: String(BROWSE_PAGE_SIZE_MAX + 1) }).params,
    ).toBeNull()
    expect(parseBrowseParams({ pageSize: '0' }).params).toBeNull()
  })

  it('accumulates one issue per invalid value', () => {
    const { issues } = parseBrowseParams({
      type: 'email',
      dir: 'sideways',
      page: 'x',
    })
    expect(issues).toHaveLength(3)
  })
})

describe('browseParamsToSearch', () => {
  it('serializes only non-default controls so URLs stay minimal', () => {
    expect(browseParamsToSearch(defaultBrowseParams)).toEqual({})
  })

  it('round-trips through parse so a copied URL restores the same view', () => {
    const params = {
      type: 'domain' as const,
      provider: 2,
      tag: 'botnet',
      lastSeenFrom: '2026-01-01T00:00:00.000Z',
      lastSeenTo: '2026-01-31T23:59:59.000Z',
      orderBy: 'firstSeenUtc' as const,
      dir: 'asc' as const,
      page: 4,
      pageSize: 100,
    }
    const search = browseParamsToSearch(params)

    expect(search).toEqual({
      type: 'domain',
      provider: '2',
      tag: 'botnet',
      lastSeenFrom: '2026-01-01T00:00:00.000Z',
      lastSeenTo: '2026-01-31T23:59:59.000Z',
      orderBy: 'firstSeenUtc',
      dir: 'asc',
      page: '4',
      pageSize: '100',
    })

    const { params: restored, issues } = parseBrowseParams(search)
    expect(issues).toEqual([])
    expect(restored).toEqual(params)
  })

  it('a default page is not serialized but page 2+ is', () => {
    expect(
      browseParamsToSearch({ ...defaultBrowseParams, page: 1 }).page,
    ).toBeUndefined()
    expect(browseParamsToSearch({ ...defaultBrowseParams, page: 2 }).page).toBe(
      '2',
    )
  })
})

describe('hasActiveBrowseFilters', () => {
  it('is false for the default view', () => {
    expect(hasActiveBrowseFilters(defaultBrowseParams)).toBe(false)
  })

  it('is true when any filter or page size differs from the default', () => {
    expect(
      hasActiveBrowseFilters({ ...defaultBrowseParams, tag: 'phishing' }),
    ).toBe(true)
    expect(
      hasActiveBrowseFilters({ ...defaultBrowseParams, type: 'url' }),
    ).toBe(true)
    expect(hasActiveBrowseFilters({ ...defaultBrowseParams, dir: 'asc' })).toBe(
      true,
    )
    expect(
      hasActiveBrowseFilters({ ...defaultBrowseParams, pageSize: 50 }),
    ).toBe(true)
  })
})
