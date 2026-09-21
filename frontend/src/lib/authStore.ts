import { create } from 'zustand'
import { queryClient } from './queryClient'
import type { LoginResponse } from './schemas'
import {
  isSessionExpired,
  readStoredSession,
  removeStoredSession,
  toStoredSession,
  writeStoredSession,
} from './session'
import type { StoredSession } from './session'

type AuthState = {
  session: StoredSession | null
  setSession: (session: StoredSession) => void
  clearSession: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  session: null,
  setSession: (session) => set({ session }),
  clearSession: () => set({ session: null }),
}))

/** Current session that is valid (present and not expired). */
export function currentSession(): StoredSession | null {
  const session = useAuthStore.getState().session
  if (!session) return null
  if (isSessionExpired(session)) return null
  return session
}

/** Stores a fresh login response in session storage and mirrors it for rendering. */
export function startSession(response: LoginResponse): StoredSession {
  const session = toStoredSession(response)
  writeStoredSession(session)
  useAuthStore.getState().setSession(session)
  return session
}

/**
 * Removes the token, the mirrored session, and every cached query (including
 * protected data) for logout, token expiry, or a 401 from a protected endpoint.
 */
export function clearAllSessionData(): void {
  removeStoredSession()
  useAuthStore.getState().clearSession()
  queryClient.clear()
}

/** Restores a valid, unexpired session from browser session storage, if any. */
export function hydrateSessionFromStorage(): StoredSession | null {
  const stored = readStoredSession()
  if (!stored) return null
  if (isSessionExpired(stored)) {
    clearAllSessionData()
    return null
  }
  useAuthStore.getState().setSession(stored)
  return stored
}

/** Logout: clears token, mirrored session, and cached queries in one step. */
export function logout(): void {
  clearAllSessionData()
}
