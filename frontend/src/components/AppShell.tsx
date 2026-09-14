import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useRouter } from '@tanstack/react-router'
import { logout, useAuthStore } from '../lib/authStore'

export const focusRingClass =
  'rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary'

const navLinkClass = `rounded px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant transition-colors hover:bg-surface-container-low hover:text-on-surface ${focusRingClass}`
const authLinkClass = `font-mono text-xs font-semibold uppercase tracking-wider text-primary underline decoration-dotted underline-offset-4 hover:text-on-surface ${focusRingClass}`

type ShellSession = { username: string; role: 'User' | 'Admin' } | null

/**
 * Primary navigation. User navigation always exposes Search and Browse; an
 * Admin session additionally exposes the Admin destination.
 */
export function ShellNav({ session }: { session: ShellSession }) {
  return (
    <nav
      aria-label="Primary"
      className="flex flex-wrap items-center gap-x-1 gap-y-1"
    >
      <Link
        to="/search"
        className={navLinkClass}
        aria-label="Search indicators"
      >
        Search
      </Link>
      <Link
        to="/browse"
        className={navLinkClass}
        aria-label="Browse indicators"
      >
        Browse
      </Link>
      {session?.role === 'Admin' ? (
        <Link to="/admin" className={navLinkClass} aria-label="Admin area">
          Admin
        </Link>
      ) : null}
    </nav>
  )
}

/** Identity area: signed-in feedback plus logout, or a sign-in link. */
export function ShellAuthArea({ session }: { session: ShellSession }) {
  const router = useRouter()

  if (!session) {
    return (
      <div className="flex items-center gap-3">
        <Link to="/login" className={authLinkClass}>
          Sign in
        </Link>
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
      <span className="text-xs text-on-surface-variant">
        Signed in as{' '}
        <span className="font-mono font-semibold text-on-surface">
          {session.username}
        </span>{' '}
        ({session.role === 'Admin' ? 'Administrator' : 'User'})
      </span>
      <button
        type="button"
        onClick={() => {
          logout()
          void router.navigate({ to: '/login' })
        }}
        className={`rounded border border-outline-variant bg-surface-container-lowest px-2.5 py-1 text-xs font-medium text-on-surface transition-colors hover:bg-surface-container-low ${focusRingClass}`}
      >
        Sign out
      </button>
    </div>
  )
}

/**
 * Responsive accessible application shell: a skip link, a header with brand,
 * primary navigation and session controls, the main landmark, and a footer.
 * The session-dependent areas render only after mount so server-rendered and
 * first client render stay identical (the session lives in session storage,
 * which only the browser can read).
 */
export function AppShell({ children }: { children: ReactNode }) {
  const session = useAuthStore((state) => state.session)
  const [mounted, setMounted] = useState(false)
  useEffect(() => setMounted(true), [])
  const visibleSession = mounted ? session : null

  return (
    <div className="flex min-h-screen flex-col bg-surface text-on-surface">
      <a
        href="#main-content"
        className={`sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-surface-container-lowest focus:px-3 focus:py-2 focus:font-medium focus:text-on-surface focus:shadow ${focusRingClass}`}
      >
        Skip to main content
      </a>
      <header className="sticky top-0 z-40 border-b border-outline-variant/50 bg-surface-container-lowest/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-3 md:px-6">
          <Link
            to="/"
            className={`flex items-center gap-2.5 ${focusRingClass}`}
            aria-label="Threat Intelligence home"
          >
            <span
              aria-hidden="true"
              className="flex h-7 w-7 items-center justify-center rounded-sm bg-primary font-mono text-[11px] font-bold tracking-tight text-on-primary"
            >
              TI
            </span>
            <span className="flex flex-col leading-none">
              <span className="text-sm font-semibold tracking-tight text-on-surface">
                Threat Intelligence
              </span>
              <span className="font-mono text-[9px] uppercase tracking-[0.18em] text-outline">
                Aggregation Console
              </span>
            </span>
          </Link>
          <ShellNav session={visibleSession} />
          <ShellAuthArea session={visibleSession} />
        </div>
      </header>
      <main
        id="main-content"
        tabIndex={-1}
        className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 md:px-6"
      >
        {children}
      </main>
      <footer className="border-t border-outline-variant/50 bg-surface-container-lowest">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 md:px-6">
          <span className="font-mono text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
            Threat Intelligence
          </span>
          <span
            aria-hidden="true"
            className="h-1 w-1 rounded-full bg-outline-variant"
          />
          <span className="text-xs text-on-surface-variant">
            Aggregated threat indicators from configured providers.
          </span>
        </div>
      </footer>
    </div>
  )
}
