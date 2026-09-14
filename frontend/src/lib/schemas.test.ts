import { iocSearchParamsSchema } from './schemas'

describe('IOC search parameters', () => {
  it('trims and validates the query at the frontend boundary', () => {
    expect(iocSearchParamsSchema.parse({ q: '  example.com  ' }).q).toBe(
      'example.com',
    )
    expect(iocSearchParamsSchema.safeParse({ q: '' }).success).toBe(false)
  })
})
