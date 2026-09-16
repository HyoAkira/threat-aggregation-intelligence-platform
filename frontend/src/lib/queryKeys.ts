import type { BrowseParams } from './browseParams'
import type { IocRelation, SearchSubmission } from './apiClient'

export const iocSearchKey = (submission: SearchSubmission) =>
  [
    'ioc-search',
    submission.q,
    submission.tag ?? null,
    submission.provider ?? null,
    submission.page ?? 1,
    submission.pageSize ?? 20,
  ] as const

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

export const adminLogsKey = (
  count: number,
  level: string | null,
  source: string,
  correlationId: string | null,
) => ['admin', 'logs', count, level, source, correlationId] as const

export const adminSettingsKey = () => ['admin', 'settings'] as const
