/**
 * Resolves the post-login destination. Deep links to protected pages are
 * remembered by the route guards as a `redirect` search parameter; this function
 * accepts only same-application paths so a tampered parameter can never turn
 * login into an open redirect, and never loops back to login itself.
 */
export function resolveLoginRedirect(
  raw: string | undefined,
  fallback = '/search',
): string {
  if (!raw) return fallback
  if (!raw.startsWith('/') || raw.startsWith('//') || raw.startsWith('/\\'))
    return '/search'
  if (
    raw === '/login' ||
    raw.startsWith('/login?') ||
    raw.startsWith('/login/')
  )
    return '/search'
  return raw
}
