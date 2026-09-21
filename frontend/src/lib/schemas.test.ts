import {
  bulkSearchResultSchema,
  iocSearchParamsSchema,
  iocSuggestionsSchema,
} from './schemas'

describe('IOC search parameters', () => {
  it('trims and validates the query at the frontend boundary', () => {
    expect(iocSearchParamsSchema.parse({ q: '  example.com  ' }).q).toBe(
      'example.com',
    )
    expect(iocSearchParamsSchema.safeParse({ q: '' }).success).toBe(false)
  })

  it('accepts bounded malware and target filters and rejects oversized ones', () => {
    const parsed = iocSearchParamsSchema.parse({
      q: 'example.com',
      malware: ' Emotet ',
      target: ' Amazon ',
    })
    expect(parsed.malware).toBe('Emotet')
    expect(parsed.target).toBe('Amazon')

    expect(
      iocSearchParamsSchema.safeParse({ q: 'x', malware: 'a'.repeat(201) })
        .success,
    ).toBe(false)
    expect(
      iocSearchParamsSchema.safeParse({ q: 'x', target: 'a'.repeat(501) })
        .success,
    ).toBe(false)
  })
})

describe('bulk search response contract', () => {
  const validResult = {
    items: [
      {
        input: '1.2.3.4',
        status: 'matched',
        groups: [],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
      {
        input: 'absent.example',
        status: 'not_found',
        groups: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        error: { code: 'ioc_not_found', detail: 'No domain IOC matches.' },
      },
    ],
    totalCount: 2,
    matchedCount: 1,
    notFoundCount: 1,
    invalidCount: 0,
  }

  it('accepts a well-formed ordered bulk envelope', () => {
    const parsed = bulkSearchResultSchema.parse(validResult)
    expect(parsed.items[0].status).toBe('matched')
    expect(parsed.items[1].error?.code).toBe('ioc_not_found')
  })

  it('rejects an unknown per-item status', () => {
    const invalid = {
      ...validResult,
      items: [{ ...validResult.items[0], status: 'pending' }],
    }
    expect(bulkSearchResultSchema.safeParse(invalid).success).toBe(false)
  })

  it('rejects a missing summary field', () => {
    const { matchedCount, ...withoutCount } = validResult
    void matchedCount
    expect(bulkSearchResultSchema.safeParse(withoutCount).success).toBe(false)
  })
})

describe('IOC suggestion contract', () => {
  it('accepts value/type entries and rejects unknown type tokens', () => {
    expect(
      iocSuggestionsSchema.parse([
        { value: 'evil.example', iocType: 'domain' },
      ]),
    ).toHaveLength(1)
    expect(
      iocSuggestionsSchema.safeParse([{ value: 'x', iocType: 'email' }])
        .success,
    ).toBe(false)
  })
})
