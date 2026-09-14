import type { ZodType } from 'zod'
import { ApiError, apiProblemSchema } from './apiErrors'
import { clearAllSessionData, useAuthStore } from './authStore'
import { browseParamsToSearch } from './browseParams'
import type { BrowseParams } from './browseParams'
import {
  iocGroupSchema,
  loginResponseSchema,
  pagedIocGroupsSchema,
} from './schemas'
import type { IocGroup, LoginResponse, PagedIocGroups } from './schemas'
import { isSessionExpired } from './session'

export { ApiError }

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

/**
 * Registers the app-level reaction to a lost session (logout, expiry, or a 401
 * from a protected endpoint): the router installs a handler that returns the
 * user to `/login` while preserving the current location for resume.
 */
export function setUnauthorizedHandler(
  handler: UnauthorizedHandler | null,
): void {
  unauthorizedHandler = handler
}

function handleSessionLost(): void {
  clearAllSessionData()
  unauthorizedHandler?.()
}

export type ApiFetchOptions = {
  schema: ZodType
  method?: 'GET' | 'POST'
  body?: unknown
  signal?: AbortSignal
  /**
   * Set for the login endpoint itself: a 401 there means bad credentials and
   * must not trigger the global session-lost redirect.
   */
  allowUnauthorized?: boolean
}

/**
 * Single typed transport for the `/api` surface. Injects the Bearer token when a
 * valid session exists, parses RFC 9457 problem details on failure, validates
 * success payloads with Zod, and funnels session loss (expiry or 401) through
 * one cleanup path.
 */
export async function apiFetch<T>(
  path: string,
  options: ApiFetchOptions,
): Promise<T> {
  const storedSession = useAuthStore.getState().session
  if (storedSession && isSessionExpired(storedSession)) {
    handleSessionLost()
    throw new ApiError(
      401,
      'session_expired',
      'Your session has expired. Please sign in again.',
    )
  }
  const session = storedSession

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (session) headers.Authorization = `Bearer ${session.token}`
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      method: options.method ?? 'GET',
      headers,
      body:
        options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError')
      throw cause
    throw new ApiError(
      0,
      'network_error',
      'The service could not be reached. Please try again.',
    )
  }

  if (!response.ok) {
    const problem = await parseProblem(response)
    if (response.status === 401 && !options.allowUnauthorized) {
      handleSessionLost()
    }
    throw new ApiError(
      response.status,
      problem?.code ?? 'http_error',
      problem?.detail ?? `The request failed with status ${response.status}.`,
      problem?.correlationId ?? undefined,
      problem ?? undefined,
    )
  }

  let payload: unknown
  try {
    payload = await response.json()
  } catch {
    throw new ApiError(
      response.status,
      'invalid_response',
      'The server response was not valid JSON.',
    )
  }

  const parsed = options.schema.safeParse(payload)
  if (!parsed.success) {
    throw new ApiError(
      response.status,
      'invalid_response',
      'The server returned an unexpected response format.',
    )
  }
  return parsed.data as T
}

async function parseProblem(response: Response) {
  try {
    const parsed = apiProblemSchema.safeParse(await response.json())
    return parsed.success ? parsed.data : null
  } catch {
    return null
  }
}

function buildQuery(
  params: Record<string, string | number | null | undefined>,
): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.set(key, String(value))
  }
  const encoded = search.toString()
  return encoded ? `?${encoded}` : ''
}

export type SearchSubmission = {
  q: string
  tag: string | null
  provider: number | null
}

/** `GET /api/iocs/search` — exact grouped search with optional tag/provider filters. */
export function searchIocs(
  submission: SearchSubmission,
  signal?: AbortSignal,
): Promise<IocGroup> {
  return apiFetch(`/iocs/search${buildQuery(submission)}`, {
    schema: iocGroupSchema,
    signal,
  })
}

/** `GET /api/iocs/{id}` — source-attributed detail for one row id. */
export function getIocDetail(
  id: number,
  signal?: AbortSignal,
): Promise<IocGroup> {
  return apiFetch(`/iocs/${id}`, { schema: iocGroupSchema, signal })
}

export type IocRelation = 'urlHost' | 'parentDomain'

/** `GET /api/iocs/{id}/relations` — one bounded page of related groups. */
export function getIocRelations(
  input: { id: number; relation: IocRelation; page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<PagedIocGroups> {
  return apiFetch(
    `/iocs/${input.id}/relations${buildQuery({
      relation: input.relation,
      page: input.page,
      pageSize: input.pageSize,
    })}`,
    { schema: pagedIocGroupsSchema, signal },
  )
}

/** `GET /api/iocs` — filtered, ordered, paginated grouped browse. */
export function browseIocs(
  params: BrowseParams,
  signal?: AbortSignal,
): Promise<PagedIocGroups> {
  return apiFetch(`/iocs${buildQuery(browseParamsToSearch(params))}`, {
    schema: pagedIocGroupsSchema,
    signal,
  })
}

/** `POST /api/auth/login` — a 401 here only means invalid credentials. */
export function login(
  username: string,
  password: string,
  signal?: AbortSignal,
): Promise<LoginResponse> {
  return apiFetch('/auth/login', {
    schema: loginResponseSchema,
    method: 'POST',
    body: { username, password },
    signal,
    allowUnauthorized: true,
  })
}
