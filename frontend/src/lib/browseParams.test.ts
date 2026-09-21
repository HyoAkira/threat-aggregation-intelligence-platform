import {
  BROWSE_MALWARE_MAX_LENGTH,
  BROWSE_PAGE_SIZE_MAX,
  BROWSE_TARGET_MAX_LENGTH,
  browseParamsToSearch,
  defaultBrowseParams,
  hasActiveBrowseFilters,
  normalizeBrowseTags,
  parseBrowseParams,
} from './browseParams'

describe('parseBrowseParams', () => {
  it('normalizes, deduplicates, and preserves multiple tag filters', () => {
    expect(normalizeBrowseTags(' BotNet, phishing, botnet ')).toEqual([
      'botnet',
      'phishing',
    ])
    expect(parseBrowseParams({ tag: ' BotNet, phishing, botnet ' })).toEqual({
      params: { ...defaultBrowseParams, tag: 'botnet,phishing' },
      issues: [],
    })
  })

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
      malware: ' Emotet ',
      target: ' Amazon ',
      orderBy: 'iocValue',
      dir: 'asc',
      page: '3',
      pageSize: '50',
    })

    expect(issues).toEqual([])
    expect(params).toEqual({
      type: 'url',
      provider: '1',
      tag: 'phishing',
      malware: 'Emotet',
      target: 'Amazon',
      lastSeenFrom: null,
      lastSeenTo: null,
      orderBy: 'iocValue',
      dir: 'asc',
      page: 3,
      pageSize: 50,
    })
  })

  it('treats blank malware and target filters as absent', () => {
    const { params, issues } = parseBrowseParams({ malware: '   ', target: '' })
    expect(issues).toEqual([])
    expect(params?.malware).toBeNull()
    expect(params?.target).toBeNull()
  })

  it('rejects an oversized malware or target filter', () => {
    const malware = parseBrowseParams({
      malware: 'a'.repeat(BROWSE_MALWARE_MAX_LENGTH + 1),
    })
    expect(malware.params).toBeNull()
    expect(malware.issues[0]).toContain('malware')

    const target = parseBrowseParams({
      target: 'a'.repeat(BROWSE_TARGET_MAX_LENGTH + 1),
    })
    expect(target.params).toBeNull()
    expect(target.issues[0]).toContain('target')
  })

  it('rejects an unsupported indicator type', () => {
    const { params, issues } = parseBrowseParams({ type: 'email' })
    expect(params).toBeNull()
    expect(issues[0]).toContain('email')
  })

  it('accepts a free-form provider name filter', () => {
    const { params, issues } = parseBrowseParams({ provider: 'ThreatFox' })
    expect(issues).toEqual([])
    expect(params?.provider).toBe('ThreatFox')
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
      provider: '2',
      tag: 'botnet',
      malware: 'Emotet',
      target: 'Amazon',
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
      malware: 'Emotet',
      target: 'Amazon',
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
    expect(
      hasActiveBrowseFilters({ ...defaultBrowseParams, malware: 'Emotet' }),
    ).toBe(true)
    expect(
      hasActiveBrowseFilters({ ...defaultBrowseParams, target: 'Amazon' }),
    ).toBe(true)
  })
})
