import { renderToString } from 'react-dom/server'
import {
  Outlet,
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router'
import { routeTree } from '../routeTree.gen'
import { queryClient } from '../lib/queryClient'

/** What a load of one URL produced: a render, or a redirect to follow. */
export type RouteLoadResult =
  | { type: 'render'; router: ReturnType<typeof createRouter> }
  | { type: 'redirect'; to?: string; search?: unknown; href?: string }

/** Memory-history router over the real route tree. */
export function createAppRouter(path = '/'): ReturnType<typeof createRouter> {
  return createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [path] }),
  })
}

type ServerResult = {
  type?: string
  redirect?: { options?: { to?: string; search?: unknown; href?: string } }
}

/**
 * Loads one URL the way server-side rendering does: `beforeLoad` guards run and
 * any redirect they throw is reported instead of being silently committed.
 */
export async function loadAppRoute(path: string): Promise<RouteLoadResult> {
  const router = createAppRouter(path)
  await router.load()
  const result = (router as unknown as { _serverResult?: ServerResult })
    ._serverResult
  if (result?.type === 'redirect') {
    return {
      type: 'redirect',
      to: result.redirect?.options?.to,
      search: result.redirect?.options?.search,
      href: result.redirect?.options?.href,
    }
  }
  return { type: 'render', router }
}

/**
 * Renders one URL of the real application route tree (including the root
 * document, shell, and providers) into static markup, following up to three
 * redirects the way a browser follows a 3xx. Query results must be seeded into
 * the shared query cache beforehand; no network calls happen in the Node test
 * environment.
 */
export async function renderAppRoute(
  path: string,
  maxRedirects = 3,
): Promise<string> {
  let current = path
  for (let hop = 0; hop <= maxRedirects; hop++) {
    const result = await loadAppRoute(current)
    if (result.type === 'redirect') {
      if (!result.href)
        throw new Error(`Redirect from ${current} has no resolvable href.`)
      current = result.href
      continue
    }
    return renderToString(<RouterProvider router={result.router as never} />)
  }
  throw new Error(`Too many redirects while rendering ${path}.`)
}

/**
 * Renders an arbitrary element that needs router context (Link, useRouter)
 * inside a minimal throwaway router with memory history.
 */
export async function renderWithRouterContext(
  element: React.ReactElement,
): Promise<string> {
  const rootRoute = createRootRoute({
    component: () => (
      <div id="test-harness">
        {element}
        <Outlet />
      </div>
    ),
  })
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: () => null,
  })
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })
  await router.load()
  return renderToString(<RouterProvider router={router as never} />)
}

/** Resets shared module state (query cache) between tests. */
export function resetClientState(): void {
  queryClient.clear()
}
