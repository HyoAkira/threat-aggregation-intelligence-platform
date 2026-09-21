import { isRedirect } from '@tanstack/react-router'
import { createSessionStorageStub, makeSession } from '../test/fixtures'
import { useAuthStore } from './authStore'
import { resolveLoginRedirect } from './loginRedirect'
import { queryClient } from './queryClient'
import { requireAdmin, requireSession } from './routeGuards'
import { SESSION_STORAGE_KEY } from './session'

function catchRedirect(action: () => void): unknown {
  try {
    action()
  } catch (thrown) {
    return thrown
  }
  return undefined
}

describe('route guards', () => {
  beforeEach(() => {
    ;(globalThis as { sessionStorage?: Storage }).sessionStorage =
      createSessionStorageStub() as Storage
    useAuthStore.setState({ session: null })
    queryClient.clear()
  })

  it('requireSession redirects an anonymous visitor to login and remembers the deep link', () => {
    const thrown = catchRedirect(() => requireSession({ href: '/admin' }))

    expect(isRedirect(thrown)).toBe(true)

    expect((thrown as any).options).toMatchObject({
      to: '/login',
      search: { redirect: '/admin' },
    })
  })

  it('requireSession clears an expired session before redirecting', () => {
    const expired = makeSession({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    useAuthStore.setState({ session: expired })
    globalThis.sessionStorage.setItem(
      SESSION_STORAGE_KEY,
      JSON.stringify(expired),
    )

    const thrown = catchRedirect(() => requireSession({ href: '/admin' }))

    expect(isRedirect(thrown)).toBe(true)
    expect(useAuthStore.getState().session).toBeNull()
    expect(globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
  })

  it('requireSession returns the session for a signed-in user', () => {
    const session = makeSession()
    useAuthStore.setState({ session })

    expect(requireSession({ href: '/admin' })).toEqual(session)
  })

  it('requireAdmin redirects a signed-in User back to Search', () => {
    useAuthStore.setState({ session: makeSession({ role: 'User' }) })

    const thrown = catchRedirect(() => requireAdmin({ href: '/admin' }))

    expect(isRedirect(thrown)).toBe(true)

    expect((thrown as any).options).toMatchObject({ to: '/search' })
  })

  it('requireAdmin returns the session for an Admin', () => {
    const session = makeSession({ role: 'Admin' })
    useAuthStore.setState({ session })

    expect(requireAdmin({ href: '/admin' })).toEqual(session)
  })
})

describe('resolveLoginRedirect', () => {
  it('defaults to the search experience', () => {
    expect(resolveLoginRedirect(undefined)).toBe('/search')
    expect(resolveLoginRedirect('')).toBe('/search')
    expect(resolveLoginRedirect(undefined, '/admin')).toBe('/admin')
  })

  it('resumes the originally requested protected page', () => {
    expect(resolveLoginRedirect('/admin')).toBe('/admin')
    expect(resolveLoginRedirect('/browse?tag=phishing&page=2')).toBe(
      '/browse?tag=phishing&page=2',
    )
  })

  it('refuses external and protocol-relative targets (open-redirect safety)', () => {
    expect(resolveLoginRedirect('https://evil.example')).toBe('/search')
    expect(resolveLoginRedirect('//evil.example')).toBe('/search')
    expect(resolveLoginRedirect('/\\evil.example')).toBe('/search')
    expect(resolveLoginRedirect('evil.example')).toBe('/search')
  })

  it('never redirects back to login itself', () => {
    expect(resolveLoginRedirect('/login')).toBe('/search')
    expect(resolveLoginRedirect('/login?redirect=/admin')).toBe('/search')
    expect(resolveLoginRedirect('/login/sub')).toBe('/search')
  })
})
