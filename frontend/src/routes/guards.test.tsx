import {
  loadAppRoute,
  renderAppRoute,
  resetClientState,
} from '../test/renderApp'
import { useAuthStore } from '../lib/authStore'
import { queryClient } from '../lib/queryClient'
import { iocDetailKey } from '../lib/queryKeys'
import { makeSession, urlGroup } from '../test/fixtures'

function setState(session: ReturnType<typeof makeSession> | null) {
  useAuthStore.setState({ session })
}

describe('route guards through the real router', () => {
  beforeEach(() => {
    resetClientState()
    setState(null)
  })

  afterEach(() => {
    resetClientState()
    setState(null)
  })

  it('an anonymous visitor can open the public IOC search page', async () => {
    const markup = await renderAppRoute('/search')

    expect(markup).toContain('Search indicators')
    expect(markup).not.toContain('Sign-in required')
  })

  it('an anonymous visitor can open the public browse and detail pages', async () => {
    const browse = await loadAppRoute('/browse')
    expect(browse.type).toBe('render')

    const detail = await loadAppRoute('/iocs/7')
    expect(detail.type).toBe('render')
  })

  it('an anonymous visitor on an Admin route is sent to login and the deep link is remembered', async () => {
    const result = await loadAppRoute('/admin')

    expect(result.type).toBe('redirect')
    if (result.type !== 'redirect') return
    expect(result.to).toBe('/login')
    expect(result.search).toEqual({ redirect: '/admin' })
    expect(result.href).toBe('/login?redirect=%2Fadmin')
  })

  it('a deep link with a query string keeps its full href through the redirect', async () => {
    const result = await loadAppRoute('/admin?tab=providers')

    expect(result.type).toBe('redirect')
    if (result.type !== 'redirect') return
    expect(result.to).toBe('/login')
    expect(result.search).toEqual({ redirect: '/admin?tab=providers' })
  })

  it('a signed-in User is redirected away from the Admin route without rendering it', async () => {
    setState(makeSession({ role: 'User' }))
    const result = await loadAppRoute('/admin')

    expect(result.type).toBe('redirect')
    if (result.type !== 'redirect') return
    expect(result.to).toBe('/search')
  })

  it('a signed-in Admin can open the Admin route', async () => {
    setState(makeSession({ role: 'Admin' }))
    const markup = await renderAppRoute('/admin')

    expect(markup).toContain('Administrative workflows')
  })

  it('login resumes the remembered protected page once a session exists (deep-link resume)', async () => {
    setState(makeSession({ role: 'Admin' }))
    const result = await loadAppRoute('/login?redirect=/admin')

    expect(result.type).toBe('redirect')
    if (result.type !== 'redirect') return
    expect(result.to).toBe('/admin')

    // Following the resume target with the session in place renders the page.
    const markup = await renderAppRoute('/admin')
    expect(markup).toContain('Administrative workflows')
  })

  it('a signed-in User opening /login is routed to the search experience', async () => {
    setState(makeSession({ role: 'User' }))
    const result = await loadAppRoute('/login')

    expect(result.type).toBe('redirect')
    if (result.type !== 'redirect') return
    expect(result.to).toBe('/search')
  })

  it('the entry route renders the public search hub instead of redirecting', async () => {
    const result = await loadAppRoute('/')

    expect(result.type).toBe('render')

    const markup = await renderAppRoute('/')
    expect(markup).toContain('Inspect Any Indicator of Compromise.')
  })
})

describe('anonymous IOC data access through public routes', () => {
  beforeEach(() => resetClientState())
  afterEach(() => resetClientState())

  it('renders detail data with no session at all', async () => {
    setState(null)
    queryClient.setQueryData(iocDetailKey(7), urlGroup)

    const markup = await renderAppRoute('/iocs/7')

    expect(markup).toContain(urlGroup.iocValue)
    expect(markup).toContain('Source: ThreatFox')
    expect(markup).toContain('Source: PhishTank')
  })
})
