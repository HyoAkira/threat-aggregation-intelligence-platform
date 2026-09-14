import { createRouter as createTanStackRouter } from '@tanstack/react-router'
import { routeTree } from './routeTree.gen'
import { hydrateSessionFromStorage } from './lib/authStore'
import { setUnauthorizedHandler } from './lib/apiClient'

export function getRouter() {
  const router = createTanStackRouter({
    routeTree,
    scrollRestoration: true,
    defaultPreload: 'intent',
    defaultPreloadStaleTime: 0,
  })

  if (typeof window !== 'undefined') {
    // Restore a valid session before the first client render, and send any
    // session loss (expiry or a 401 from a protected endpoint) back to login
    // while remembering the current location for post-login resume.
    hydrateSessionFromStorage()
    setUnauthorizedHandler(() => {
      const { pathname, href } = router.state.location
      const redirect = pathname === '/login' ? undefined : href
      void router.navigate({
        to: '/login',
        search: redirect ? { redirect } : undefined,
      })
    })
  }

  return router
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
