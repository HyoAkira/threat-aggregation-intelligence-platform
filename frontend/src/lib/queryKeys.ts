import type { BrowseParams } from './browseParams'
import type {
  BulkSearchSubmission,
  IocRelation,
  SearchSubmission,
} from './apiClient'

export const iocSearchKey = (submission: SearchSubmission) =>
  [
    'ioc-search',
    submission.q,
    submission.tag ?? null,
    submission.provider ?? null,
    submission.malware ?? null,
    submission.target ?? null,
    submission.page ?? 1,
    submission.pageSize ?? 20,
  ] as const

export const iocSearchBulkKey = (submission: BulkSearchSubmission) =>
  [
    'ioc-search-bulk',
    submission.malware ?? null,
    submission.target ?? null,
    submission.queries,
    submission.tag ?? null,
    submission.provider ?? null,
    submission.page,
    submission.pageSize,
  ] as const

export const iocSuggestionsKey = (query: string) =>
  ['ioc-suggestions', query] as const

export const iocDetailKey = (id: number) => ['ioc-detail', id] as const

export const iocRelationsKey = (
  id: number,
  relation: IocRelation | 'none',
  page: number,
) => ['ioc-relations', id, relation, page] as const

export const browseKey = (params: BrowseParams) =>
  [
    'ioc-browse',
    params.type,
    params.provider,
    params.tag,
    params.lastSeenFrom,
    params.lastSeenTo,
    params.orderBy,
    params.dir,
    params.page,
    params.pageSize,
  ] as const

export const iocTagCatalogKey = (query: string) =>
  ['ioc-tag-catalog', query] as const

export const iocProviderNamesKey = () => ['ioc-provider-names'] as const

/* -------------------------------------------------------------------------- */
/*  Admin query keys                                                         */
/* -------------------------------------------------------------------------- */

export const adminProviderListKey = () => ['admin', 'providers'] as const

export const adminProviderDetailKey = (id: number) =>
  ['admin', 'providers', id] as const

export const adminHistoryKey = (
  providerId: number,
  page: number,
  pageSize: number,
) => ['admin', 'providers', providerId, 'history', page, pageSize] as const

export const adminFetchJobKey = (jobId: string) =>
  ['admin', 'fetch-job', jobId] as const

export const adminSummaryKey = () => ['admin', 'summary'] as const

/**
 * Timeline query key carries the full range/timezone context so a new timezone or
 * range never reuses points from the previous context as current.
 */
export const adminTimelineKey = (from: string, to: string, timeZone: string) =>
  ['admin', 'summary', 'timeline', from, to, timeZone] as const

/** Narrow prefix used to invalidate every open timeline range at once. */
export const adminTimelineKeyPrefix = () =>
  ['admin', 'summary', 'timeline'] as const

export const adminLogsKey = (
  count: number,
  level: string | null,
  source: string,
  correlationId: string | null,
) => ['admin', 'logs', count, level, source, correlationId] as const

export const adminSettingsKey = () => ['admin', 'settings'] as const

export const adminCredentialKey = (identity: string) =>
  ['admin', 'credential', identity] as const

export const adminAiSettingsKey = () => ['admin', 'ai', 'settings'] as const

export const adminIocChatSettingsKey = () =>
  ['admin', 'ioc-chat', 'settings'] as const

export const adminAiReportPolicyKey = () =>
  ['admin', 'ai', 'report-policy'] as const
