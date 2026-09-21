import {
  ApiError,
  apiFetch,
  browseIocs,
  getAdminTimeline,
  getIocDetail,
  getIocRelations,
  getIocSuggestions,
  getIocTagCatalog,
  getIocMetadataCatalog,
  login,
  searchIocs,
  searchIocsBulk,
  setUnauthorizedHandler,
} from './apiClient'
import { queryClient } from './queryClient'
import { useAuthStore } from './authStore'
import { SESSION_STORAGE_KEY } from './session'
import {
  iocGroupSchema,
  pagedIocGroupsSchema,
  loginResponseSchema,
} from './schemas'
import { defaultBrowseParams } from './browseParams'
import {
  createSessionStorageStub,
  makePaged,
  makeSession,
  urlGroup,
} from '../test/fixtures'

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response
}

const problemBody = (
  status: number,
  code: string,
  detail: string,
  correlationId = 'corr-123',
) => ({
  type: `urn:threat-intelligence:problem:${code}`,
  title: 'Title',
  status,
  detail,
  code,
  correlationId,
})

describe('apiFetch transport', () => {
  let fetchMock: jest.Mock
  let unauthorizedHandler: jest.Mock

  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
    fetchMock = jest.fn()
    globalThis.fetch = fetchMock
    unauthorizedHandler = jest.fn()
    setUnauthorizedHandler(unauthorizedHandler)
  })

  afterEach(() => {
    setUnauthorizedHandler(null)
  })

  it('injects the Bearer token when a valid session exists', async () => {
    useAuthStore.setState({ session: makeSession({ token: 'jwt-token' }) })
    fetchMock.mockResolvedValue(jsonResponse(urlGroup))

    await apiFetch('/iocs/search?q=x', { schema: iocGroupSchema })

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/iocs/search?q=x')
    expect((init.headers as Record<string, string>).Authorization).toBe(
      'Bearer jwt-token',
    )
  })

  it('sends no Authorization header without a session (anonymous IOC access)', async () => {
    fetchMock.mockResolvedValue(jsonResponse(urlGroup))

    await apiFetch('/iocs/search?q=x', { schema: iocGroupSchema })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(
      (init.headers as Record<string, string>).Authorization,
    ).toBeUndefined()
  })

  it('parses RFC 9457 problem details into a typed ApiError', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(problemBody(400, 'invalid_input', 'Not an indicator.'), 400),
    )

    await expect(
      apiFetch('/iocs/search?q=x', { schema: iocGroupSchema }),
    ).rejects.toMatchObject({
      status: 400,
      code: 'invalid_input',
      correlationId: 'corr-123',
      message: 'Not an indicator.',
    })
  })

  it('a 401 on a protected endpoint clears session and cache and notifies the handler', async () => {
    const session = makeSession()
    useAuthStore.setState({ session })
    globalThis.sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(session),
    )
    queryClient.setQueryData(['protected'], 'data')
    fetchMock.mockResolvedValue(
      jsonResponse(
        problemBody(401, 'unauthenticated', 'Authentication is required.'),
        401,
      ),
    )

    await expect(
      apiFetch('/admin/providers', { schema: iocGroupSchema }),
    ).rejects.toBeInstanceOf(ApiError)

    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
    expect(queryClient.getQueryData(['protected'])).toBeUndefined()
    expect(unauthorizedHandler).toHaveBeenCalledTimes(1)
  })

  it('a 401 from the login endpoint does not trigger the session-lost handler', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        problemBody(401, 'unauthenticated', 'Invalid credentials.'),
        401,
      ),
    )

    await expect(
      apiFetch('/auth/login', {
        schema: loginResponseSchema,
        allowUnauthorized: true,
      }),
    ).rejects.toMatchObject({ status: 401, code: 'unauthenticated' })

    expect(unauthorizedHandler).not.toHaveBeenCalled()
  })

  it('an expired session is detected before the request and treated as session loss', async () => {
    const expired = makeSession({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    useAuthStore.setState({ session: expired })
    globalThis.sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(expired),
    )
    queryClient.setQueryData(['protected'], 'data')

    await expect(
      apiFetch('/iocs', { schema: pagedIocGroupsSchema }),
    ).rejects.toMatchObject({
      code: 'session_expired',
    })

    expect(fetchMock).not.toHaveBeenCalled()
    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
    expect(queryClient.getQueryData(['protected'])).toBeUndefined()
    expect(unauthorizedHandler).toHaveBeenCalledTimes(1)
  })

  it('rejects success payloads that fail schema validation', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ unexpected: 'shape' }))

    await expect(
      apiFetch('/iocs/search?q=x', { schema: iocGroupSchema }),
    ).rejects.toMatchObject({
      code: 'invalid_response',
    })
  })

  it('maps network failures to a retryable transport error', async () => {
    fetchMock.mockRejectedValue(new TypeError('fetch failed'))

    await expect(
      apiFetch('/iocs', { schema: pagedIocGroupsSchema }),
    ).rejects.toMatchObject({
      code: 'network_error',
    })
  })
})

describe('IOC tag catalog client', () => {
  it('requests the bounded normalized tag catalog', async () => {
    const fetchMock = jest
      .fn()
      .mockResolvedValue(jsonResponse([{ name: 'phishing', usageCount: 4 }]))
    globalThis.fetch = fetchMock

    await expect(getIocTagCatalog({ q: 'phi', limit: 8 })).resolves.toEqual([
      { name: 'phishing', usageCount: 4 },
    ])
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/iocs/tags?q=phi&limit=8',
      expect.objectContaining({ method: 'GET' }),
    )
  })
})

const bulkResult = {
  items: [
    {
      input: '1.2.3.4',
      status: 'matched',
      groups: [urlGroup],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    },
  ],
  totalCount: 1,
  matchedCount: 1,
  notFoundCount: 0,
  invalidCount: 0,
}

describe('IOC bulk search and suggestion clients', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
  })

  it('searchIocsBulk posts the ordered queries, filters, and page bounds', async () => {
    const fetchMock = jest.fn().mockResolvedValue(jsonResponse(bulkResult))
    globalThis.fetch = fetchMock

    const parsed = await searchIocsBulk({
      queries: ['1.2.3.4', 'evil.example'],
      tag: 'botnet',
      provider: '1',
      page: 2,
      pageSize: 20,
    })

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/iocs/search/bulk')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({
      queries: ['1.2.3.4', 'evil.example'],
      tag: 'botnet',
      provider: '1',
      page: 2,
      pageSize: 20,
    })
    expect((init.headers as Record<string, string>)['Content-Type']).toBe(
      'application/json',
    )
    expect(parsed.items[0].status).toBe('matched')
  })

  it('searchIocsBulk rejects a response whose shape is invalid', async () => {
    const fetchMock = jest
      .fn()
      .mockResolvedValue(jsonResponse({ items: [{ status: 'matched' }] }))
    globalThis.fetch = fetchMock

    await expect(
      searchIocsBulk({
        queries: ['1.2.3.4'],
        tag: null,
        provider: null,
        page: 1,
        pageSize: 20,
      }),
    ).rejects.toMatchObject({ code: 'invalid_response' })
  })

  it('getIocSuggestions encodes the prefix and limit', async () => {
    const fetchMock = jest
      .fn()
      .mockResolvedValue(
        jsonResponse([{ value: 'evil.example', iocType: 'domain' }]),
      )
    globalThis.fetch = fetchMock

    await expect(
      getIocSuggestions({ q: 'evil.example', limit: 8 }),
    ).resolves.toEqual([{ value: 'evil.example', iocType: 'domain' }])
    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/suggestions?q=evil.example&limit=8',
    )
  })
})

describe('typed query functions', () => {
  let fetchMock: jest.Mock

  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
    fetchMock = jest.fn()
    globalThis.fetch = fetchMock
  })

  it.each(['malware', 'target'] as const)(
    'loads the %s prefix catalog with bounded results',
    async (kind) => {
      fetchMock.mockResolvedValue(
        jsonResponse([{ name: 'example', usageCount: 2 }]),
      )
      const result = await getIocMetadataCatalog(kind, { q: 'ex', limit: 8 })
      expect(fetchMock.mock.calls[0][0]).toBe(
        `/api/iocs/${kind === 'target' ? 'targets' : 'malware'}?q=ex&limit=8`,
      )
      expect(result).toEqual([{ name: 'example', usageCount: 2 }])
    },
  )

  it('searchIocs sends q plus optional tag and provider filters', async () => {
    fetchMock.mockResolvedValue(jsonResponse(makePaged([urlGroup], 1, 20, 1)))

    await searchIocs({ q: 'evil.example', tag: 'phishing', provider: '1' })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/search?q=evil.example&tag=phishing&provider=1',
    )
  })

  it('searchIocs sends optional malware and target filters', async () => {
    fetchMock.mockResolvedValue(jsonResponse(makePaged([urlGroup], 1, 20, 1)))

    await searchIocs({
      q: 'evil.example',
      tag: null,
      provider: null,
      malware: 'Emotet',
      target: 'Amazon',
    })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/search?q=evil.example&malware=Emotet&target=Amazon',
    )
  })

  it('searchIocs omits absent filters', async () => {
    fetchMock.mockResolvedValue(jsonResponse(makePaged([urlGroup], 1, 20, 1)))

    await searchIocs({
      q: 'evil.example',
      tag: null,
      provider: null,
      malware: null,
      target: null,
    })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/iocs/search?q=evil.example')
  })

  it('getIocDetail requests the row id', async () => {
    fetchMock.mockResolvedValue(jsonResponse(urlGroup))

    await getIocDetail(7)

    expect(fetchMock.mock.calls[0][0]).toBe('/api/iocs/7')
  })

  it('getIocRelations sends relation and pagination bounds', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({
        items: [urlGroup],
        page: 2,
        pageSize: 20,
        totalCount: 45,
      }),
    )

    await getIocRelations({ id: 7, relation: 'urlHost', page: 2, pageSize: 20 })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/7/relations?relation=urlHost&page=2&pageSize=20',
    )
  })

  it('getIocRelations accepts the malware and target kinds', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
    )

    await getIocRelations({ id: 7, relation: 'malware', page: 1, pageSize: 20 })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/7/relations?relation=malware&page=1&pageSize=20',
    )
  })

  it('browseIocs serializes only non-default controls', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
    )

    await browseIocs(defaultBrowseParams)

    expect(fetchMock.mock.calls[0][0]).toBe('/api/iocs')
  })

  it('browseIocs serializes every active control', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
    )

    await browseIocs({
      type: 'url',
      provider: '1',
      tag: 'phishing',
      malware: 'Emotet',
      target: 'Amazon',
      lastSeenFrom: '2026-01-01T00:00:00.000Z',
      lastSeenTo: '2026-01-31T23:59:59.000Z',
      orderBy: 'iocValue',
      dir: 'asc',
      page: 3,
      pageSize: 50,
    })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs?type=url&provider=1&tag=phishing&malware=Emotet&target=Amazon&lastSeenFrom=2026-01-01T00%3A00%3A00.000Z&lastSeenTo=2026-01-31T23%3A59%3A59.000Z&orderBy=iocValue&dir=asc&page=3&pageSize=50',
    )
  })

  it('login posts credentials as JSON', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({
        token: 'jwt-token',
        username: 'analyst',
        role: 'User',
        expiresAt: '2026-01-01T12:00:00Z',
      }),
    )

    await login('analyst', 'secret')

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/auth/login')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({
      username: 'analyst',
      password: 'secret',
    })
    expect((init.headers as Record<string, string>)['Content-Type']).toBe(
      'application/json',
    )
  })
})

describe('admin timeline client', () => {
  let fetchMock: jest.Mock

  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
    fetchMock = jest.fn()
    globalThis.fetch = fetchMock
  })

  it('sends the encoded UTC range and timezone and validates the response', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({
        from: '2026-09-14T00:00:00.000Z',
        to: '2026-09-21T00:00:00.000Z',
        timeZone: 'Europe/Istanbul',
        hasSuccessfulFetches: false,
        points: [],
      }),
    )

    const timeline = await getAdminTimeline({
      from: '2026-09-14T00:00:00.000Z',
      to: '2026-09-21T00:00:00.000Z',
      timeZone: 'Europe/Istanbul',
    })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/admin/summary/timeline?from=2026-09-14T00%3A00%3A00.000Z&to=2026-09-21T00%3A00%3A00.000Z&timeZone=Europe%2FIstanbul',
    )
    expect(timeline.hasSuccessfulFetches).toBe(false)
  })

  it('rejects a malformed timeline payload', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({
        from: '2026-09-14T00:00:00.000Z',
        to: '2026-09-21T00:00:00.000Z',
        timeZone: 'UTC',
        hasSuccessfulFetches: true,
        points: [{ localDate: 'bad-date', recordsAdded: 1 }],
      }),
    )

    await expect(
      getAdminTimeline({
        from: '2026-09-14T00:00:00.000Z',
        to: '2026-09-21T00:00:00.000Z',
        timeZone: 'UTC',
      }),
    ).rejects.toBeInstanceOf(ApiError)
  })
})
