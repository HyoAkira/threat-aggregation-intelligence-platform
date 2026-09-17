import { z } from 'zod'
import type { ZodType } from 'zod'
import { ApiError, apiProblemSchema } from './apiErrors'
import { clearAllSessionData, useAuthStore } from './authStore'
import { browseParamsToSearch } from './browseParams'
import type { BrowseParams } from './browseParams'
import {
  iocGroupSchema,
  iocProviderNamesSchema,
  loginResponseSchema,
  pagedIocGroupsSchema,
} from './schemas'
import type { IocGroup, LoginResponse, PagedIocGroups } from './schemas'
import { isSessionExpired } from './session'
import {
  adminProviderListSchema,
  adminProviderHistoryPageSchema,
  adminFetchHistoryItemSchema,
  adminSummarySchema,
  adminLogListSchema,
  adminLogPageSchema,
  settingsResponseSchema,
  fetchQueuedResponseSchema,
  credentialStatusSchema,
} from './adminSchemas'
import type {
  AdminProviderDashboardItem,
  AdminProviderHistoryPage,
  AdminFetchHistoryItem,
  AdminSummary,
  AdminLogEntry,
  AdminLogPage,
  SettingsResponse,
  SettingsRequest,
  FetchQueuedResponse,
  CredentialStatus,
} from './adminSchemas'

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
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
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
  if (response.status === 204) {
    payload = undefined
  } else {
    try {
      payload = await response.json()
    } catch {
      throw new ApiError(
        response.status,
        'invalid_response',
        'The server response was not valid JSON.',
      )
    }
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
  provider: string | null
  page?: number
  pageSize?: number
}

/** `GET /api/iocs/search` — grouped search, including host/domain relations. */
export function searchIocs(
  submission: SearchSubmission,
  signal?: AbortSignal,
): Promise<PagedIocGroups> {
  return apiFetch(`/iocs/search${buildQuery(submission)}`, {
    schema: pagedIocGroupsSchema,
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

/** `GET /api/iocs/providers` — public provider-name catalog for browse filters. */
export function getIocProviderNames(signal?: AbortSignal): Promise<string[]> {
  return apiFetch('/iocs/providers', {
    schema: iocProviderNamesSchema,
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

/* -------------------------------------------------------------------------- */
/*  Admin API functions                                                      */
/* -------------------------------------------------------------------------- */

/** `GET /api/admin/providers` — full provider dashboard list. */
export function getAdminProviders(
  signal?: AbortSignal,
): Promise<AdminProviderDashboardItem[]> {
  return apiFetch('/admin/providers', {
    schema: adminProviderListSchema,
    signal,
  })
}

/** `POST /api/admin/providers/{id}/fetch` — default mode manual fetch. */
export function triggerDefaultFetch(
  providerId: number,
  signal?: AbortSignal,
): Promise<FetchQueuedResponse> {
  return apiFetch(`/admin/providers/${providerId}/fetch`, {
    schema: fetchQueuedResponseSchema,
    method: 'POST',
    signal,
  })
}

/** `POST /api/admin/providers/{id}/fetch/full` — full mode manual fetch. */
export function triggerFullFetch(
  providerId: number,
  signal?: AbortSignal,
): Promise<FetchQueuedResponse> {
  return apiFetch(`/admin/providers/${providerId}/fetch/full`, {
    schema: fetchQueuedResponseSchema,
    method: 'POST',
    signal,
  })
}

/** `GET /api/admin/fetch-history/{jobId}` — single job status. */
export function getFetchJobStatus(
  jobId: string,
  signal?: AbortSignal,
): Promise<AdminFetchHistoryItem> {
  return apiFetch(`/admin/fetch-history/${jobId}`, {
    schema: adminFetchHistoryItemSchema,
    signal,
  })
}

/** `GET /api/admin/providers/{id}/history` — paginated fetch history. */
export function getAdminProviderHistory(
  providerId: number,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
  filters?: {
    dateFrom?: string | null
    dateTo?: string | null
    orderBy?: 'startedAt' | 'finishedAt'
    orderDir?: 'asc' | 'desc'
  },
): Promise<AdminProviderHistoryPage> {
  return apiFetch(
    `/admin/providers/${providerId}/history${buildQuery({
      page,
      pageSize,
      dateFrom: filters?.dateFrom,
      dateTo: filters?.dateTo,
      orderBy: filters?.orderBy,
      orderDir: filters?.orderDir,
    })}`,
    { schema: adminProviderHistoryPageSchema, signal },
  )
}

/** `GET /api/admin/logs/table` — server-side DataTables page. */
export function getAdminLogsTable(
  params: {
    page: number
    pageSize: number
    level: string | null
    source: 'api' | 'worker' | 'all'
    correlationId: string | null
    dateFrom?: string | null
    dateTo?: string | null
    orderDir: 'asc' | 'desc'
  },
  signal?: AbortSignal,
): Promise<AdminLogPage> {
  return apiFetch<AdminLogPage>(`/admin/logs/table${buildQuery(params)}`, {
    schema: adminLogPageSchema,
    signal,
  }).catch((error: unknown): Promise<AdminLogPage> => {
    // Keep the admin page usable while an already-running API process is still
    // serving the pre-DataTables build. The next request uses the new endpoint
    // automatically once the API is restarted.
    if (!(error instanceof ApiError) || error.status !== 404) throw error

    return getAdminLogs(
      500,
      params.level,
      params.source,
      params.correlationId,
      signal,
    ).then((entries) => {
      const dateFrom = adminDateBoundary(params.dateFrom)
      const dateTo = adminDateBoundary(params.dateTo)
      const filtered = entries.filter((entry) => {
        const timestamp = new Date(entry.timestamp).getTime()
        return (
          Number.isFinite(timestamp) &&
          (dateFrom === null || timestamp >= dateFrom) &&
          (dateTo === null || timestamp < dateTo)
        )
      })
      const ordered = [...filtered].sort((left, right) => {
        const difference =
          new Date(left.timestamp).getTime() -
          new Date(right.timestamp).getTime()
        return params.orderDir === 'asc' ? difference : -difference
      })
      const start = (params.page - 1) * params.pageSize
      return {
        items: ordered.slice(start, start + params.pageSize),
        page: params.page,
        pageSize: params.pageSize,
        totalCount: ordered.length,
      }
    })
  })
}

function adminDateBoundary(value: string | null | undefined): number | null {
  if (!value) return null
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return null
  return parsed.getTime()
}

/** `GET /api/admin/summary` — database IOC counts. */
export function getAdminSummary(signal?: AbortSignal): Promise<AdminSummary> {
  return apiFetch('/admin/summary', { schema: adminSummarySchema, signal })
}

/** `GET /api/admin/logs` — structured log tail. */
export function getAdminLogs(
  count: number,
  level: string | null,
  source: 'api' | 'worker' | 'all' = 'api',
  correlationId: string | null = null,
  signal?: AbortSignal,
): Promise<AdminLogEntry[]> {
  return apiFetch(
    `/admin/logs${buildQuery({ count, level, source, correlationId })}`,
    {
      schema: adminLogListSchema,
      signal,
    },
  )
}

/** `GET /api/admin/settings` — current non-secret runtime settings. */
export function getAdminSettings(
  signal?: AbortSignal,
): Promise<SettingsResponse> {
  return apiFetch('/admin/settings', {
    schema: settingsResponseSchema,
    signal,
  })
}

/** `PUT /api/admin/settings` — atomic full-snapshot settings update. */
export function putAdminSettings(
  body: SettingsRequest,
  signal?: AbortSignal,
): Promise<void> {
  return apiFetch('/admin/settings', {
    schema: z.undefined(),
    method: 'PUT',
    body,
    signal,
  })
}

export function getAdminCredential(identity: string, signal?: AbortSignal): Promise<CredentialStatus> {
  return apiFetch(`/admin/credentials/${encodeURIComponent(identity)}`, {
    schema: credentialStatusSchema,
    signal,
  })
}

export function putAdminCredential(identity: string, value: string, signal?: AbortSignal): Promise<CredentialStatus> {
  return apiFetch(`/admin/credentials/${encodeURIComponent(identity)}`, {
    schema: credentialStatusSchema,
    method: 'PUT',
    body: { value },
    signal,
  })
}

export function clearAdminCredential(identity: string, signal?: AbortSignal): Promise<void> {
  return apiFetch(`/admin/credentials/${encodeURIComponent(identity)}`, {
    schema: z.undefined(),
    method: 'DELETE',
    signal,
  })
}
