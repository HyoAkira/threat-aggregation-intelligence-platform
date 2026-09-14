import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useRouter } from '@tanstack/react-router'
import { logout, useAuthStore } from '../lib/authStore'

export const focusRingClass =
  'rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700'

const navLinkClass = `px-1 py-0.5 font-medium text-slate-700 hover:text-slate-950 ${focusRingClass}`
const authLinkClass = `font-medium text-sky-800 underline underline-offset-2 ${focusRingClass}`

type ShellSession = { username: string; role: 'User' | 'Admin' } | null

/**
 * Primary navigation. User navigation always exposes Search and Browse; an
 * Admin session additionally exposes the Admin destination.
 */
export function ShellNav({ session }: { session: ShellSession }) {
  return (
    <nav
      aria-label="Primary"
      className="flex flex-wrap items-center gap-x-5 gap-y-1"
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
      <span className="text-sm text-slate-700">
        Signed in as <span className="font-medium">{session.username}</span> (
        {session.role === 'Admin' ? 'Administrator' : 'User'})
      </span>
      <button
        type="button"
        onClick={() => {
          logout()
          void router.navigate({ to: '/login' })
        }}
        className={`rounded-md border border-slate-400 px-2.5 py-1 text-sm font-medium text-slate-800 hover:bg-slate-100 ${focusRingClass}`}
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
    <div className="flex min-h-screen flex-col">
      <a
        href="#main-content"
        className={`sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-white focus:px-3 focus:py-2 focus:font-medium focus:text-slate-900 focus:shadow ${focusRingClass}`}
      >
        Skip to main content
      </a>
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-3 md:px-6">
          <Link
            to="/"
            className={`text-lg font-semibold text-slate-900 ${focusRingClass}`}
            aria-label="Threat Intelligence home"
          >
            Threat Intelligence
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
      <footer className="border-t border-slate-200 bg-white">
        <div className="mx-auto max-w-6xl px-4 py-3 text-sm text-slate-600 md:px-6">
          Aggregated threat indicators from configured providers.
        </div>
      </footer>
    </div>
  )
}
