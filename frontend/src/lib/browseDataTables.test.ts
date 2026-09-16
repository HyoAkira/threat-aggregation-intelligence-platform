import { defaultBrowseParams } from './browseParams'
import type { BrowseParams } from './browseParams'
import { makePaged, urlGroup, ipv4Group } from '../test/fixtures'
import {
  adaptBrowseRequest,
  adaptBrowseResponse,
  browseOrderableFields,
  browseOrderFieldByName,
  browseOrderFieldByIndex,
  browseOrderIndexByField,
  browseTableColumns,
  emptyDataTablesResponse,
} from './browseDataTables'

const active: BrowseParams = {
  type: 'url',
  provider: '3',
  tag: 'phishing',
  lastSeenFrom: '2026-01-01T00:00:00.000Z',
  lastSeenTo: '2026-01-31T23:59:59.000Z',
  orderBy: 'lastSeenUtc',
  dir: 'desc',
  page: 1,
  pageSize: 20,
}

describe('supported-column mapping boundaries', () => {
  it('allows only the four endpoint ordering fields', () => {
    expect(browseOrderableFields).toEqual([
      'iocValue',
      'iocType',
      'firstSeenUtc',
      'lastSeenUtc',
    ])
    expect(new Set(browseOrderableFields).size).toBe(
      browseOrderableFields.length,
    )
  })

  it('marks providers, threat labels, and actions non-orderable', () => {
    for (const name of ['threatLabel', 'providers', 'actions']) {
      const column = browseTableColumns.find((entry) => entry.name === name)
      expect(column?.orderable).toBe(false)
      expect(column?.orderBy).toBeNull()
    }
    expect(browseOrderFieldByName.threatLabel).toBeUndefined()
    expect(browseOrderFieldByName.providers).toBeUndefined()
    expect(browseOrderFieldByName.actions).toBeUndefined()
  })

  it('disables searching for every column', () => {
    const searchFlags = browseTableColumns.map((column) => column.searchable)
    expect(searchFlags).toEqual(searchFlags.map(() => false))
  })

  it('round-trips field, index, and name maps', () => {
    for (const field of browseOrderableFields) {
      const index = browseOrderIndexByField[field]
      expect(browseOrderFieldByIndex[index]).toBe(field)
      expect(browseOrderFieldByName[field]).toBe(field)
    }
  })
})

describe('adaptBrowseRequest', () => {
  it('derives the initial page from a zero start', () => {
    const result = adaptBrowseRequest(
      { draw: 1, start: 0, length: 20, order: [] },
      active,
    )
    expect(result).toEqual({
      ok: true,
      draw: 1,
      params: {
        ...active,
        orderBy: 'lastSeenUtc',
        dir: 'desc',
        page: 1,
        pageSize: 20,
      },
    })
  })

  it('derives a nonzero page from a nonzero start', () => {
    const result = adaptBrowseRequest(
      { draw: 4, start: 40, length: 20, order: [] },
      active,
    )
    if (!result.ok) throw new Error('expected a valid adaptation')
    expect(result.params.page).toBe(3)
    expect(result.params.pageSize).toBe(20)
  })

  it('accepts every supported page size and preserves active filters', () => {
    for (const pageSize of [10, 20, 50, 100]) {
      const result = adaptBrowseRequest(
        { draw: 1, start: pageSize * 2, length: pageSize, order: [] },
        active,
      )
      if (!result.ok) throw new Error('expected a valid adaptation')
      expect(result.params.pageSize).toBe(pageSize)
      expect(result.params.page).toBe(3)
      expect(result.params.type).toBe('url')
      expect(result.params.provider).toBe('3')
      expect(result.params.tag).toBe('phishing')
      expect(result.params.lastSeenFrom).toBe(active.lastSeenFrom)
      expect(result.params.lastSeenTo).toBe(active.lastSeenTo)
    }
  })

  it('maps every supported sort field by column index and name', () => {
    const cases = [
      { index: 0, field: 'iocValue' },
      { index: 1, field: 'iocType' },
      { index: 4, field: 'firstSeenUtc' },
      { index: 5, field: 'lastSeenUtc' },
    ] as const
    for (const { index, field } of cases) {
      const byIndex = adaptBrowseRequest(
        {
          draw: 1,
          start: 0,
          length: 20,
          order: [{ column: index, dir: 'asc' }],
        },
        active,
      )
      const byName = adaptBrowseRequest(
        { draw: 1, start: 0, length: 20, order: [{ name: field, dir: 'asc' }] },
        active,
      )
      if (!byIndex.ok || !byName.ok)
        throw new Error('expected valid adaptations')
      expect(byIndex.params.orderBy).toBe(field)
      expect(byIndex.params.dir).toBe('asc')
      expect(byName.params.orderBy).toBe(field)
    }
  })

  it('keeps the active ordering for unsupported columns', () => {
    const result = adaptBrowseRequest(
      { draw: 1, start: 0, length: 20, order: [{ column: 3, dir: 'asc' }] },
      active,
    )
    if (!result.ok) throw new Error('expected a valid adaptation')
    expect(result.params.orderBy).toBe('lastSeenUtc')
    expect(result.params.dir).toBe('desc')
  })

  it('reads only the first of multiple order terms', () => {
    const result = adaptBrowseRequest(
      {
        draw: 1,
        start: 0,
        length: 20,
        order: [
          { column: 0, dir: 'asc' },
          { column: 1, dir: 'desc' },
        ],
      },
      active,
    )
    if (!result.ok) throw new Error('expected a valid adaptation')
    expect(result.params.orderBy).toBe('iocValue')
    expect(result.params.dir).toBe('asc')
  })

  it('rejects length=-1 and other invalid lengths', () => {
    for (const length of [-1, 0, 101, 1.5, Number.NaN]) {
      const result = adaptBrowseRequest(
        { draw: 7, start: 0, length, order: [] },
        active,
      )
      expect(result).toEqual({
        ok: false,
        draw: 7,
        reason: 'unsupported_length',
      })
    }
  })

  it('rejects a missing or non-integer draw', () => {
    expect(adaptBrowseRequest({ start: 0, length: 20 }, active)).toEqual({
      ok: false,
      draw: 0,
      reason: 'invalid_draw',
    })
    expect(
      adaptBrowseRequest({ draw: 1.5, start: 0, length: 20 }, active),
    ).toEqual({ ok: false, draw: 0, reason: 'invalid_draw' })
  })

  it('rejects negative or fractional starts', () => {
    for (const start of [-1, 2.5, Number.NaN]) {
      expect(
        adaptBrowseRequest({ draw: 3, start, length: 20 }, active),
      ).toEqual({ ok: false, draw: 3, reason: 'invalid_start' })
    }
  })
})

describe('adaptBrowseResponse', () => {
  it('echoes the draw and maps the matching count to both count fields', () => {
    const result = adaptBrowseResponse(
      12,
      makePaged([urlGroup, ipv4Group], 1, 20, 45),
    )
    expect(result).toEqual({
      draw: 12,
      data: [urlGroup, ipv4Group],
      recordsTotal: 45,
      recordsFiltered: 45,
    })
  })

  it('handles an empty successful response', () => {
    const result = adaptBrowseResponse(2, makePaged([], 1, 20, 0))
    expect(result.data).toEqual([])
    expect(result.recordsTotal).toBe(0)
    expect(result.recordsFiltered).toBe(0)
  })

  it('handles filtered and large totals', () => {
    const filtered = adaptBrowseResponse(5, makePaged([urlGroup], 2, 10, 100))
    expect(filtered.recordsTotal).toBe(100)
    expect(filtered.data).toEqual([urlGroup])

    const large = adaptBrowseResponse(6, makePaged([], 1, 100, 2_000_000))
    expect(large.recordsTotal).toBe(2_000_000)
    expect(large.recordsFiltered).toBe(2_000_000)
  })

  it('emptyDataTablesResponse settles a draw without rows', () => {
    expect(emptyDataTablesResponse(9)).toEqual({
      draw: 9,
      data: [],
      recordsTotal: 0,
      recordsFiltered: 0,
    })
  })
})

describe('defaults stay orderable-safe', () => {
  it('default browse params use a supported ordering field', () => {
    expect(browseOrderableFields).toContain(defaultBrowseParams.orderBy)
  })
})
