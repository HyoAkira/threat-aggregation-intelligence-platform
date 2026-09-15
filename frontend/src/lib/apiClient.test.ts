import {
  ApiError,
  apiFetch,
  browseIocs,
  getIocDetail,
  getIocRelations,
  login,
  searchIocs,
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

  it('searchIocs sends q plus optional tag and provider filters', async () => {
    fetchMock.mockResolvedValue(jsonResponse(makePaged([urlGroup], 1, 20, 1)))

    await searchIocs({ q: 'evil.example', tag: 'phishing', provider: '1' })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs/search?q=evil.example&tag=phishing&provider=1',
    )
  })

  it('searchIocs omits absent filters', async () => {
    fetchMock.mockResolvedValue(jsonResponse(makePaged([urlGroup], 1, 20, 1)))

    await searchIocs({ q: 'evil.example', tag: null, provider: null })

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
      lastSeenFrom: '2026-01-01T00:00:00.000Z',
      lastSeenTo: '2026-01-31T23:59:59.000Z',
      orderBy: 'iocValue',
      dir: 'asc',
      page: 3,
      pageSize: 50,
    })

    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/iocs?type=url&provider=1&tag=phishing&lastSeenFrom=2026-01-01T00%3A00%3A00.000Z&lastSeenTo=2026-01-31T23%3A59%3A59.000Z&orderBy=iocValue&dir=asc&page=3&pageSize=50',
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
