import { z } from 'zod'
import type { LoginResponse } from './schemas'

export const SESSION_STORAGE_KEY = 'ti.session'

/** Active session as persisted in browser session storage and mirrored in Zustand. */
export type StoredSession = {
  token: string
  username: string
  role: 'User' | 'Admin'
  /** ISO 8601 instant from the login response. */
  expiresAt: string
}

const storedSessionSchema = z.object({
  token: z.string().min(1),
  username: z.string().min(1),
  role: z.enum(['User', 'Admin']),
  expiresAt: z.string().min(1),
})

export function toStoredSession(response: LoginResponse): StoredSession {
  return {
    token: response.token,
    username: response.username,
    role: response.role,
    expiresAt: response.expiresAt,
  }
}

function hasSessionStorage(): boolean {
  return typeof globalThis.sessionStorage !== 'undefined'
}

/** Reads and validates the stored session; malformed entries are treated as absent. */
export function readStoredSession(): StoredSession | null {
  if (!hasSessionStorage()) return null
  try {
    const raw = globalThis.sessionStorage.getItem(SESSION_STORAGE_KEY)
    if (!raw) return null
    const parsed = storedSessionSchema.safeParse(JSON.parse(raw))
    return parsed.success ? parsed.data : null
  } catch {
    return null
  }
}

export function writeStoredSession(session: StoredSession): void {
  if (!hasSessionStorage()) return
  globalThis.sessionStorage.setItem(
    SESSION_STORAGE_KEY,
    JSON.stringify(session),
  )
}

export function removeStoredSession(): void {
  if (!hasSessionStorage()) return
  globalThis.sessionStorage.removeItem(SESSION_STORAGE_KEY)
}

/**
 * A session is expired once the current UTC time is at or past `expiresAt`.
 * The boundary itself counts as expired so a token is never used past its life.
 */
export function isSessionExpired(
  session: StoredSession,
  now: Date = new Date(),
): boolean {
  const expiry = Date.parse(session.expiresAt)
  if (Number.isNaN(expiry)) return true
  return now.getTime() >= expiry
}
