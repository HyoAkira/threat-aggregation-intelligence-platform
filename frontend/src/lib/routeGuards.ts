import { redirect } from '@tanstack/react-router'
import { clearAllSessionData, currentSession } from './authStore'
import type { StoredSession } from './session'

/**
 * Guard for protected routes: requires a current (present and unexpired)
 * session. An expired or missing session is cleared and the requested location
 * is remembered so login can resume the deep link. The API remains authoritative;
 * these guards only complement server authorization.
 */
export function requireSession(location: { href: string }): StoredSession {
  const session = currentSession()
  if (!session) {
    clearAllSessionData()
    throw redirect({ to: '/login', search: { redirect: location.href } })
  }
  return session
}

/**
 * Guard for Admin routes: requires a current session that carries the Admin
 * role. A signed-in User is sent back to Search without rendering admin data.
 */
export function requireAdmin(location: { href: string }): StoredSession {
  const session = requireSession(location)
  if (session.role !== 'Admin') {
    throw redirect({ to: '/search' })
  }
  return session
}
