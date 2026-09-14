import { queryClient } from './queryClient'
import {
  useAuthStore,
  clearAllSessionData,
  hydrateSessionFromStorage,
  startSession,
  logout,
} from './authStore'
import {
  SESSION_STORAGE_KEY,
  isSessionExpired,
  readStoredSession,
} from './session'
import { createSessionStorageStub, makeSession } from '../test/fixtures'

describe('session expiry', () => {
  it('treats the expiry boundary as expired', () => {
    const expiresAt = '2026-01-01T12:00:00Z'
    expect(
      isSessionExpired(
        makeSession({ expiresAt }),
        new Date('2026-01-01T11:59:59Z'),
      ),
    ).toBe(false)
    expect(
      isSessionExpired(
        makeSession({ expiresAt }),
        new Date('2026-01-01T12:00:00Z'),
      ),
    ).toBe(true)
    expect(
      isSessionExpired(
        makeSession({ expiresAt }),
        new Date('2026-01-01T12:00:01Z'),
      ),
    ).toBe(true)
  })

  it('treats an unparseable expiry as expired', () => {
    expect(isSessionExpired(makeSession({ expiresAt: 'not-a-date' }))).toBe(
      true,
    )
  })
})

describe('session lifecycle', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
  })

  it('starts a session: storage write plus Zustand mirror', () => {
    startSession({
      token: 'jwt-token',
      username: 'analyst',
      role: 'User',
      expiresAt: new Date(Date.now() + 3600_000).toISOString(),
    })

    expect(readStoredSession()?.username).toBe('analyst')
    expect(useAuthStore.getState().session?.username).toBe('analyst')
  })

  it('logout clears the token, the mirrored session, and the query cache', () => {
    startSession({
      token: 'jwt-token',
      username: 'analyst',
      role: 'Admin',
      expiresAt: new Date(Date.now() + 3600_000).toISOString(),
    })
    queryClient.setQueryData(['protected'], 'cached-value')
    expect(queryClient.getQueryData(['protected'])).toBe('cached-value')

    logout()

    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
    expect(queryClient.getQueryData(['protected'])).toBeUndefined()
  })

  it('clearAllSessionData removes cached protected data', () => {
    useAuthStore.setState({ session: makeSession() })
    queryClient.setQueryData(['ioc-browse'], { items: [] })
    globalThis.sessionStorage.setItem(SESSION_STORAGE_KEY, 'stale')

    clearAllSessionData()

    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
    expect(queryClient.getQueryData(['ioc-browse'])).toBeUndefined()
  })

  it('hydrates a valid stored session into the store', () => {
    const session = makeSession()
    globalThis.sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(session),
    )

    const hydrated = hydrateSessionFromStorage()

    expect(hydrated?.username).toBe('analyst')
    expect(useAuthStore.getState().session?.username).toBe('analyst')
  })

  it('drops an expired stored session and clears cached data', () => {
    const expired = makeSession({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    globalThis.sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(expired),
    )
    queryClient.setQueryData(['protected'], 'cached-value')

    const hydrated = hydrateSessionFromStorage()

    expect(hydrated).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
    expect(queryClient.getQueryData(['protected'])).toBeUndefined()
  })

  it('ignores malformed stored sessions', () => {
    globalThis.sessionStorage.setItem(SESSION_STORAGE_KEY, '{not-json')

    expect(hydrateSessionFromStorage()).toBeNull()
    expect(useAuthStore.getState().session).toBeNull()
  })
})
